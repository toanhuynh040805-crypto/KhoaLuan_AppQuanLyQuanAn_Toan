import { authApi } from '../api/authApi.js?auth-session-2';
import {
    clearCustomerSession,
    saveCustomerSession,
    setCurrentCustomer
} from '../api/session.js?auth-session-2';
import { profileApi } from '../api/profileApi.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { API_BASE_URL } from '../api/config.js';

// ======================================================
// 1. KHAI BÁO CÁC PHẦN TỬ GIAO DIỆN
// ======================================================

const loginTab = document.querySelector('#tab-login');
const registerTab = document.querySelector('#tab-register');

const loginForm = document.querySelector('#form-login');
const registerForm = document.querySelector('#form-register');
const forgotForm = document.querySelector('#form-forgot-password');

const globalStatus = document.querySelector('[data-auth-status]');
const registerStatus = document.querySelector('[data-register-status]');
const registerButton = document.getElementById('btn-register');

const btnShowForgot = document.getElementById('btn-show-forgot');
const btnBackToLogin = document.getElementById('btn-back-to-login');
const btnGoogleLogin = document.getElementById('btn-google-login');

let isLoginSubmitting = false;
let isRegisterSubmitting = false;

if (!loginTab || !registerTab || !loginForm || !registerForm) {
    throw new Error('Không tìm thấy form đăng nhập hoặc đăng ký trên trang.');
}

setupSessionNavigation();

// ======================================================
// 2. CÁC HÀM HỖ TRỢ
// ======================================================

// 2.1. Quản lý ghi nhớ đăng nhập (Remember Me)
const REMEMBER_LOGIN_KEY = 'gusto_remembered_login';
const REMEMBER_ENABLED_KEY = 'gusto_remember_me_enabled';

function getRememberedLogin() {
    try {
        return localStorage.getItem(REMEMBER_LOGIN_KEY) || '';
    } catch {
        return '';
    }
}

function isRememberMeEnabled() {
    try {
        const flag = localStorage.getItem(REMEMBER_ENABLED_KEY);
        if (flag === 'false') return false;
        if (flag === 'true') return true;
        // Nếu đã từng có thông tin đăng nhập được ghi nhớ thì mặc định bật
        if (getRememberedLogin()) return true;
        return true;
    } catch {
        return true;
    }
}

function saveRememberedLogin(identifier) {
    try {
        if (typeof identifier === 'string' && identifier.trim()) {
            localStorage.setItem(REMEMBER_LOGIN_KEY, identifier.trim());
            localStorage.setItem(REMEMBER_ENABLED_KEY, 'true');
        }
    } catch {
        // Storage may be unavailable
    }
}

function clearRememberedLogin() {
    try {
        localStorage.removeItem(REMEMBER_LOGIN_KEY);
        localStorage.setItem(REMEMBER_ENABLED_KEY, 'false');
    } catch {
        // Storage may be unavailable
    }
}

function setupRememberMeListeners() {
    const rememberCheckbox = loginForm?.elements?.rememberMe;
    if (!rememberCheckbox) return;

    rememberCheckbox.addEventListener('change', (event) => {
        if (!event.target.checked) {
            clearRememberedLogin();
        } else {
            try {
                localStorage.setItem(REMEMBER_ENABLED_KEY, 'true');
                const currentIdentifier = loginForm.elements.login?.value?.trim();
                if (currentIdentifier) {
                    localStorage.setItem(REMEMBER_LOGIN_KEY, currentIdentifier);
                }
            } catch {
                // Storage may be unavailable
            }
        }
    });
}

async function restoreRememberedLogin() {
    const rememberCheckbox = loginForm?.elements?.rememberMe;
    const loginInput = loginForm?.elements?.login;
    const passwordInput = loginForm?.elements?.password;

    if (!rememberCheckbox || !loginInput) return;

    const enabled = isRememberMeEnabled();
    rememberCheckbox.checked = enabled;

    if (!enabled) {
        return;
    }

    const remembered = getRememberedLogin();
    if (remembered) {
        loginInput.value = remembered;
        loginInput.dispatchEvent(new Event('input', { bubbles: true }));
        loginInput.dispatchEvent(new Event('change', { bubbles: true }));

        // Hỗ trợ Password Manager: Khi tài khoản đã được tự điền,
        // nếu đang ở desktop và ô mật khẩu đang trống, tự động focus
        // để trình duyệt kích hoạt gợi ý / tự điền mật khẩu đã lưu.
        const isTouchDevice = 'ontouchstart' in window || (navigator.maxTouchPoints && navigator.maxTouchPoints > 0);
        if (!isTouchDevice && passwordInput && !passwordInput.value) {
            setTimeout(() => {
                if (
                    document.activeElement === document.body ||
                    !document.activeElement ||
                    document.activeElement === loginInput
                ) {
                    passwordInput.focus();
                }
            }, 100);
        }
    }

    // Tích hợp Credential Management API (silent retrieval) nếu trình duyệt hỗ trợ
    if (typeof window.PasswordCredential === 'function' && navigator.credentials?.get) {
        try {
            const cred = await navigator.credentials.get({
                password: true,
                mediation: 'silent'
            });
            if (cred && cred.id) {
                if (!loginInput.value) {
                    loginInput.value = cred.id;
                    loginInput.dispatchEvent(new Event('input', { bubbles: true }));
                }
                if (passwordInput && cred.password && !passwordInput.value) {
                    passwordInput.value = cred.password;
                    passwordInput.dispatchEvent(new Event('input', { bubbles: true }));
                }
            }
        } catch {
            // Không hỗ trợ hoặc bị từ chối silent retrieval, giữ nguyên cơ chế autofill chuẩn
        }
    }
}

function getInputForField(form, fieldKey) {
    const map = {
        login: form.querySelector('#login-email'),
        password: form.querySelector('#login-password'),
        fullName: form.querySelector('#reg-name'),
        phone: form.querySelector('#reg-phone'),
        email: form.querySelector('#reg-email'),
        regPassword: form.querySelector('#reg-password'),
        confirmPassword: form.querySelector('#reg-confirm-password'),
        terms: form.querySelector('#reg-terms')
    };

    return map[fieldKey] || null;
}

const DEFAULT_RETURN_URL = '/pages/profile/index.html';

function getSafeReturnUrl() {
    const requested = new URLSearchParams(location.search).get('returnUrl');

    if (!requested) {
        return DEFAULT_RETURN_URL;
    }

    try {
        const target = new URL(requested, location.href);

        // Chỉ chuyển đến trang cùng website để tránh open redirect.
        if (target.origin !== location.origin) {
            return DEFAULT_RETURN_URL;
        }

        // Không cho phép chuyển hướng ngược lại trang đăng nhập/đăng ký
        if (target.pathname.includes('login_register')) {
            return DEFAULT_RETURN_URL;
        }

        // Tự động chuẩn hóa nếu thiếu tiền tố /pages
        let path = target.pathname;
        if (path === '/profile/index.html' || path.startsWith('/profile/')) {
            path = `/pages${path}`;
        }

        return `${path}${target.search}${target.hash}`;
    } catch {
        return DEFAULT_RETURN_URL;
    }
}

function setInputErrorState(input, isError) {
    if (!input) return;

    input.classList.toggle('border-error', isError);
    input.classList.toggle('border-outline-variant', !isError);
    input.classList.toggle('focus:border-error', isError);
    input.classList.toggle('focus:border-primary', !isError);

    if (isError) {
        input.style.borderColor = '#ba1a1a';
        input.setAttribute('aria-invalid', 'true');
    } else {
        input.style.borderColor = '';
        input.removeAttribute('aria-invalid');
    }
}

function setFieldError(form, fieldKey, message) {
    const errorNode = form.querySelector(`[data-error="${fieldKey}"]`);

    if (errorNode) {
        errorNode.textContent = message || '';
        if (message) {
            errorNode.classList.remove('hidden');
        }
    }

    const input = getInputForField(form, fieldKey);

    if (input) {
        setInputErrorState(input, Boolean(message));
    }
}

function getEmailPattern() {
    return /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+$/;
}

function showGlobalStatus(message) {
    if (globalStatus) {
        globalStatus.textContent = message || '';
        if (message) {
            globalStatus.classList.remove('hidden');
        }
    }
}

function showRegisterStatus(message) {
    if (registerStatus) {
        registerStatus.textContent = message || '';
        if (message) {
            registerStatus.classList.remove('hidden');
        }
    }
}

// ======================================================
// 3. KIỂM TRA DỮ LIỆU ĐĂNG NHẬP
// Hỗ trợ username, email và số điện thoại.
// ======================================================

function validateLoginIdentifier(value) {
    const trimmed = (value || '').trim();

    if (!trimmed) {
        return 'Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.';
    }

    // Có ký tự @ thì kiểm tra theo định dạng email.
    if (trimmed.includes('@')) {
        return getEmailPattern().test(trimmed)
            ? ''
            : 'Email không đúng định dạng.';
    }

    // Không cho phép khoảng trắng hoặc ký tự điều khiển.
    if (/\s/.test(trimmed) || /[\u0000-\u001F\u007F]/.test(trimmed)) {
        return 'Tên đăng nhập không được chứa khoảng trắng hoặc ký tự không hợp lệ.';
    }

    // Nếu chỉ chứa chữ số thì kiểm tra như số điện thoại.
    if (/^\d+$/.test(trimmed)) {
        if (trimmed.length < 9 || trimmed.length > 11) {
            return 'Số điện thoại phải có từ 9 đến 11 chữ số.';
        }

        return '';
    }

    // Tên tài khoản
    if (trimmed.length > 100) {
        return 'Tên đăng nhập không được vượt quá 100 ký tự.';
    }

    return '';
}

function validateLoginPassword(value) {
    if (!value) {
        return 'Vui lòng nhập mật khẩu.';
    }

    return '';
}

// ======================================================
// 4. KIỂM TRA DỮ LIỆU ĐĂNG KÝ
// ======================================================

function validateFullName(value) {
    const trimmed = (value || '').trim();

    if (!trimmed) {
        return 'Vui lòng nhập họ và tên.';
    }

    if (trimmed.length < 2) {
        return 'Họ và tên phải có ít nhất 2 ký tự.';
    }

    return '';
}

function validateRegisterPhone(value) {
    const trimmed = (value || '').trim();

    if (!trimmed) {
        return 'Số điện thoại không được để trống.';
    }

    if (!/^\d+$/.test(trimmed)) {
        return 'Số điện thoại chỉ được chứa chữ số.';
    }

    if (trimmed.length < 9 || trimmed.length > 11) {
        return 'Số điện thoại phải có từ 9 đến 11 chữ số.';
    }

    return '';
}

function validateRegisterEmail(value) {
    const trimmed = (value || '').trim();

    // Email đăng ký được phép để trống.
    if (!trimmed) {
        return '';
    }

    return getEmailPattern().test(trimmed)
        ? ''
        : 'Email không đúng định dạng.';
}

function validateRegisterPassword(value) {
    if (!value) {
        return 'Vui lòng nhập mật khẩu.';
    }

    if (
        value.length < 8 ||
        value.length > 128 ||
        !/[A-Za-z]/.test(value) ||
        !/\d/.test(value)
    ) {
        return 'Mật khẩu phải có từ 8 đến 128 ký tự, bao gồm 1 chữ cái và 1 chữ số.';
    }

    return '';
}

function validateConfirmPassword(password, confirmPassword) {
    if (!confirmPassword) {
        return 'Vui lòng xác nhận mật khẩu.';
    }

    if (password !== confirmPassword) {
        return 'Mật khẩu xác nhận không trùng khớp.';
    }

    return '';
}

function validateTerms(isChecked) {
    if (!isChecked) {
        return 'Vui lòng đồng ý với điều khoản dịch vụ và chính sách bảo mật.';
    }

    return '';
}

// ======================================================
// 5. CHUYỂN ĐỔI GIỮA ĐĂNG NHẬP, ĐĂNG KÝ VÀ QUÊN MẬT KHẨU
// ======================================================

function switchTab(targetMode) {
    const isLogin = targetMode === 'login';
    const isRegister = targetMode === 'register';
    const isForgot = targetMode === 'forgot';

    loginTab.classList.toggle('border-primary', isLogin || isForgot);
    loginTab.classList.toggle('text-primary', isLogin || isForgot);
    loginTab.classList.toggle(
        'text-on-surface-variant',
        !isLogin && !isForgot
    );

    registerTab.classList.toggle('border-primary', isRegister);
    registerTab.classList.toggle('text-primary', isRegister);
    registerTab.classList.toggle(
        'text-on-surface-variant',
        !isRegister
    );

    loginTab.setAttribute(
        'aria-selected',
        String(isLogin || isForgot)
    );

    registerTab.setAttribute(
        'aria-selected',
        String(isRegister)
    );

    loginForm.classList.toggle('hidden', !isLogin);
    registerForm.classList.toggle('hidden', !isRegister);

    if (forgotForm) {
        forgotForm.classList.toggle('hidden', !isForgot);
    }

    showGlobalStatus('');
    showRegisterStatus('');
}

if (btnShowForgot) {
    btnShowForgot.addEventListener('click', (event) => {
        event.preventDefault();
        switchTab('forgot');
    });
}

if (btnBackToLogin) {
    btnBackToLogin.addEventListener('click', (event) => {
        event.preventDefault();
        switchTab('login');
    });
}

loginTab.addEventListener('click', () => {
    switchTab('login');
});

registerTab.addEventListener('click', () => {
    switchTab('register');
});

// ======================================================
// 6. HIỆN / ẨN MẬT KHẨU
// ======================================================

document.querySelectorAll('[data-password-toggle]').forEach((button) => {
    button.addEventListener('click', () => {
        const input = document.getElementById(
            button.dataset.passwordToggle
        );

        if (!input) return;

        const nextType = input.type === 'password'
            ? 'text'
            : 'password';

        input.type = nextType;

        const visible = nextType === 'text';

        button.setAttribute(
            'aria-label',
            visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'
        );

        const icon = button.querySelector('.material-symbols-outlined');

        if (icon) {
            icon.textContent = visible
                ? 'visibility_off'
                : 'visibility';
        }
    });
});

// ======================================================
// 7. KIỂM TRA TRẠNG THÁI NÚT ĐĂNG KÝ
// ======================================================

function updateRegisterButtonState() {
    if (!registerButton) return;
    if (!isRegisterSubmitting) {
        registerButton.disabled = false;
        registerButton.classList.remove('opacity-50', 'cursor-not-allowed');
    }
}

function clearRegisterErrors() {
    [
        'fullName',
        'phone',
        'email',
        'regPassword',
        'confirmPassword',
        'terms'
    ].forEach((fieldKey) => {
        setFieldError(registerForm, fieldKey, '');
    });

    showRegisterStatus('');
}

// ======================================================
// 8. XỬ LÝ NHẬP LIỆU ĐĂNG NHẬP
// ======================================================

loginForm.elements.login.addEventListener('input', () => {
    const value = loginForm.elements.login.value;

    if (value.trim()) {
        setFieldError(
            loginForm,
            'login',
            validateLoginIdentifier(value)
        );
    } else {
        setFieldError(loginForm, 'login', '');
    }

    showGlobalStatus('');
});

loginForm.elements.login.addEventListener('blur', () => {
    setFieldError(
        loginForm,
        'login',
        validateLoginIdentifier(loginForm.elements.login.value)
    );
});

loginForm.elements.password.addEventListener('input', () => {
    if (loginForm.elements.password.value) {
        setFieldError(loginForm, 'password', '');
    }

    showGlobalStatus('');
});

loginForm.elements.password.addEventListener('blur', () => {
    setFieldError(
        loginForm,
        'password',
        validateLoginPassword(loginForm.elements.password.value)
    );
});

// ======================================================
// 9. XỬ LÝ ĐĂNG NHẬP
// ======================================================

loginForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (isLoginSubmitting) return;

    const identifier = loginForm.elements.login.value.trim();
    const password = loginForm.elements.password.value;

    const identifierError = validateLoginIdentifier(identifier);
    const passwordError = validateLoginPassword(password);

    setFieldError(loginForm, 'login', identifierError);
    setFieldError(loginForm, 'password', passwordError);

    if (identifierError || passwordError) {
        if (identifierError) {
            loginForm.elements.login.focus();
        } else {
            loginForm.elements.password.focus();
        }
        return;
    }

    const submitButton = loginForm.querySelector('button[type="submit"]');
    const originalButtonContent = submitButton ? submitButton.innerHTML : 'Đăng nhập';

    isLoginSubmitting = true;
    if (submitButton) {
        submitButton.disabled = true;
        submitButton.innerHTML = '<span class="material-symbols-outlined animate-spin text-lg">progress_activity</span><span>Đang đăng nhập...</span>';
    }

    showGlobalStatus('');

    try {
        // Gửi tên đăng nhập, email hoặc số điện thoại cùng mật khẩu
        const response = await authApi.login(identifier, password);

        // Xử lý Ghi nhớ đăng nhập an toàn
        const rememberMeChecked = Boolean(loginForm.elements.rememberMe?.checked);
        if (rememberMeChecked) {
            saveRememberedLogin(identifier);

            // Báo cho Password Manager của trình duyệt lưu/cập nhật thông tin đăng nhập
            if (typeof window.PasswordCredential === 'function' && navigator.credentials?.store) {
                try {
                    const cred = new PasswordCredential({
                        id: identifier,
                        password: password,
                        name: identifier
                    });
                    navigator.credentials.store(cred).catch(() => {});
                } catch {
                    // Môi trường không hỗ trợ PasswordCredential
                }
            }
        } else {
            clearRememberedLogin();
            if (navigator.credentials?.preventSilentAccess) {
                try {
                    navigator.credentials.preventSilentAccess().catch(() => {});
                } catch {}
            }
        }

        // Lưu phiên đăng nhập do backend trả về
        saveCustomerSession(response, rememberMeChecked);

        // Lấy thông tin tài khoản đang đăng nhập
        try {
            const customer = await profileApi.getMyProfile();
            setCurrentCustomer(customer);
        } catch (error) {
            // Không được chuyển sang Profile nếu Backend từ chối chính phiên vừa đăng nhập.
            // Trước đây lỗi 401/403 bị nuốt nên token bị client xóa nhưng trang vẫn chuyển,
            // gây hiện tượng Profile nháy một lần rồi quay lại Login.
            if (error?.status === 401 || error?.status === 403) {
                throw error;
            }
            // Lỗi phụ không liên quan xác thực (ví dụ mạng chập chờn) vẫn cho phép
            // giữ phiên để người dùng có thể thử tải lại ở trang tiếp theo.
        }

        globalThis.dispatchEvent(new CustomEvent('gusto:session-updated'));

        // Chuyển đến trang tiếp theo sau khi đăng nhập thành công
        location.assign(getSafeReturnUrl());
    } catch (error) {
        clearCustomerSession();

        const status = error?.status;
        const code = error?.code || '';
        const msg = (error?.message || '').trim();
        const lowerMsg = msg.toLowerCase();

        // 1. Lỗi mạng / mất kết nối server
        if (code === 'NETWORK_ERROR' || status === 0) {
            showGlobalStatus('Không thể kết nối máy chủ. Hãy kiểm tra kết nối mạng rồi thử lại.');
            return;
        }

        // 2. Lỗi máy chủ 500
        if (status === 500) {
            showGlobalStatus('Máy chủ đang gặp sự cố. Vui lòng thử lại sau.');
            return;
        }

        // 3. Lỗi sai mật khẩu (Backend trả về code WRONG_PASSWORD hoặc message "Mật khẩu không chính xác")
        if (code === 'WRONG_PASSWORD' || lowerMsg.includes('mật khẩu không chính xác') || lowerMsg.includes('sai mật khẩu')) {
            setFieldError(loginForm, 'password', 'Mật khẩu không chính xác.');
            loginForm.elements.password.focus();
            return;
        }

        // 4. Lỗi tài khoản không tồn tại / chưa cài mật khẩu
        if (code === 'ACCOUNT_NOT_FOUND' || lowerMsg.includes('tài khoản không tồn tại')) {
            setFieldError(loginForm, 'login', 'Tài khoản không tồn tại hoặc chưa cài đặt mật khẩu.');
            loginForm.elements.login.focus();
            return;
        }

        // 5. Lỗi 401 Unauthorized chung từ Backend
        if (status === 401) {
            showGlobalStatus('Tài khoản hoặc mật khẩu không chính xác. Vui lòng thử lại.');
            return;
        }

        // 6. Trường cụ thể nếu có
        if (error.field) {
            setFieldError(loginForm, error.field, msg);
            return;
        }

        // Mặc định
        showGlobalStatus(msg || 'Đăng nhập không thành công. Vui lòng thử lại.');
    } finally {
        isLoginSubmitting = false;
        if (submitButton) {
            submitButton.disabled = false;
            submitButton.innerHTML = originalButtonContent;
        }
    }
});

// ======================================================
// 10. KIỂM TRA NHẬP LIỆU ĐĂNG KÝ
// ======================================================

[
    'fullName',
    'phone',
    'email',
    'password',
    'confirmPassword'
].forEach((fieldKey) => {
    const input = getInputForField(registerForm, fieldKey);

    if (!input) return;

    input.addEventListener('input', () => {
        if (fieldKey === 'fullName') {
            const err = validateFullName(registerForm.elements.fullName.value);
            if (!err) setFieldError(registerForm, 'fullName', '');
        }

        if (fieldKey === 'phone') {
            const err = validateRegisterPhone(registerForm.elements.phone.value);
            if (!err) setFieldError(registerForm, 'phone', '');
        }

        if (fieldKey === 'email') {
            const err = validateRegisterEmail(registerForm.elements.email.value);
            if (!err) setFieldError(registerForm, 'email', '');
        }

        if (fieldKey === 'password') {
            const err = validateRegisterPassword(registerForm.elements.password.value);
            if (!err) setFieldError(registerForm, 'regPassword', '');

            if (registerForm.elements.confirmPassword.value) {
                const confErr = validateConfirmPassword(
                    registerForm.elements.password.value,
                    registerForm.elements.confirmPassword.value
                );
                if (!confErr) setFieldError(registerForm, 'confirmPassword', '');
            }
        }

        if (fieldKey === 'confirmPassword') {
            const confErr = validateConfirmPassword(
                registerForm.elements.password.value,
                registerForm.elements.confirmPassword.value
            );
            if (!confErr) setFieldError(registerForm, 'confirmPassword', '');
        }

        updateRegisterButtonState();
    });

    input.addEventListener('blur', () => {
        if (fieldKey === 'fullName') {
            const val = registerForm.elements.fullName.value.trim();
            if (val) setFieldError(registerForm, 'fullName', validateFullName(val));
        }

        if (fieldKey === 'phone') {
            const val = registerForm.elements.phone.value.trim();
            if (val) setFieldError(registerForm, 'phone', validateRegisterPhone(val));
        }

        if (fieldKey === 'email') {
            const val = registerForm.elements.email.value.trim();
            if (val) setFieldError(registerForm, 'email', validateRegisterEmail(val));
        }

        if (fieldKey === 'password') {
            const val = registerForm.elements.password.value;
            if (val) setFieldError(registerForm, 'regPassword', validateRegisterPassword(val));
        }

        if (fieldKey === 'confirmPassword') {
            const val = registerForm.elements.confirmPassword.value;
            if (val) {
                setFieldError(
                    registerForm,
                    'confirmPassword',
                    validateConfirmPassword(registerForm.elements.password.value, val)
                );
            }
        }

        updateRegisterButtonState();
    });
});

registerForm.elements.terms.addEventListener('change', () => {
    if (registerForm.elements.terms.checked) {
        setFieldError(registerForm, 'terms', '');
    } else {
        setFieldError(registerForm, 'terms', validateTerms(false));
    }

    updateRegisterButtonState();
});

// ======================================================
// 11. XỬ LÝ ĐĂNG KÝ
// ======================================================

registerForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (isRegisterSubmitting) return;

    const fullName = registerForm.elements.fullName.value.trim();
    const phone = registerForm.elements.phone.value.trim();
    const email = registerForm.elements.email.value.trim();
    const password = registerForm.elements.password.value;
    const confirmation = registerForm.elements.confirmPassword.value;
    const termsAccepted = registerForm.elements.terms.checked;

    const fullNameError = validateFullName(fullName);
    const phoneError = validateRegisterPhone(phone);
    const emailError = validateRegisterEmail(email);
    const passwordError = validateRegisterPassword(password);
    const confirmError = validateConfirmPassword(password, confirmation);
    const termsError = validateTerms(termsAccepted);

    setFieldError(registerForm, 'fullName', fullNameError);
    setFieldError(registerForm, 'phone', phoneError);
    setFieldError(registerForm, 'email', emailError);
    setFieldError(registerForm, 'regPassword', passwordError);
    setFieldError(registerForm, 'confirmPassword', confirmError);
    setFieldError(registerForm, 'terms', termsError);

    // Focus vào ô lỗi đầu tiên để người dùng dễ thao tác
    if (fullNameError) {
        registerForm.elements.fullName.focus();
        return;
    }
    if (phoneError) {
        registerForm.elements.phone.focus();
        return;
    }
    if (emailError) {
        registerForm.elements.email.focus();
        return;
    }
    if (passwordError) {
        registerForm.elements.password.focus();
        return;
    }
    if (confirmError) {
        registerForm.elements.confirmPassword.focus();
        return;
    }
    if (termsError) {
        registerForm.elements.terms.focus();
        return;
    }

    const submitButton = registerForm.querySelector('button[type="submit"]');
    const originalButtonContent = submitButton ? submitButton.innerHTML : 'Tạo tài khoản';

    isRegisterSubmitting = true;
    if (submitButton) {
        submitButton.disabled = true;
        submitButton.innerHTML = '<span class="material-symbols-outlined animate-spin text-lg">progress_activity</span><span>Đang tạo tài khoản...</span>';
    }

    showRegisterStatus('');

    try {
        await authApi.register({
            fullName,
            phone,
            email: email || null,
            password
        });

        // Đăng ký thành công: chuyển sang tab đăng nhập và điền sẵn SĐT/Email
        showGlobalStatus('Tài khoản đã được tạo thành công! Hãy đăng nhập để tiếp tục.');
        switchTab('login');
        if (loginForm.elements.login) {
            loginForm.elements.login.value = phone || email || '';
            loginForm.elements.password.value = '';
            loginForm.elements.password.focus();
        }
    } catch (error) {
        const status = error?.status;
        const code = error?.code || '';
        const msg = (error?.message || '').trim();
        const field = (error?.field || '').toLowerCase();
        const errors = error?.errors;
        const lowerMsg = msg.toLowerCase();

        // 1. Lỗi mạng
        if (code === 'NETWORK_ERROR' || status === 0) {
            showRegisterStatus('Không thể kết nối máy chủ. Vui lòng kiểm tra mạng rồi thử lại.');
            return;
        }

        // 2. Lỗi máy chủ 500
        if (status === 500) {
            showRegisterStatus('Máy chủ đang gặp sự cố. Vui lòng thử lại sau.');
            return;
        }

        // 3. Nếu backend trả về field cụ thể
        if (field) {
            if (field === 'phone') {
                setFieldError(registerForm, 'phone', msg || 'Số điện thoại này đã được sử dụng.');
                registerForm.elements.phone.focus();
                return;
            }
            if (field === 'email') {
                setFieldError(registerForm, 'email', msg || 'Email này đã được sử dụng.');
                registerForm.elements.email.focus();
                return;
            }
            if (field === 'password' || field === 'regpassword') {
                setFieldError(registerForm, 'regPassword', msg);
                registerForm.elements.password.focus();
                return;
            }
            if (field === 'fullname') {
                setFieldError(registerForm, 'fullName', msg);
                registerForm.elements.fullName.focus();
                return;
            }
        }

        // 4. Nếu backend trả về ModelState errors dictionary
        if (errors && typeof errors === 'object') {
            let matchedField = false;
            for (const [key, val] of Object.entries(errors)) {
                const errorText = Array.isArray(val) ? val[0] : String(val);
                const lk = key.toLowerCase();
                if (lk.includes('phone') || lk.includes('sdt')) {
                    setFieldError(registerForm, 'phone', errorText);
                    matchedField = true;
                } else if (lk.includes('email')) {
                    setFieldError(registerForm, 'email', errorText);
                    matchedField = true;
                } else if (lk.includes('password') || lk.includes('matkhau')) {
                    setFieldError(registerForm, 'regPassword', errorText);
                    matchedField = true;
                } else if (lk.includes('name') || lk.includes('ten')) {
                    setFieldError(registerForm, 'fullName', errorText);
                    matchedField = true;
                }
            }
            if (matchedField) return;
        }

        // 5. Phân tích nội dung message thực tế từ Backend
        // 5a. Trùng số điện thoại
        if (code === 'PHONE_EXISTS' || (lowerMsg.includes('số điện thoại') && !lowerMsg.includes('email'))) {
            setFieldError(registerForm, 'phone', 'Số điện thoại này đã được sử dụng.');
            registerForm.elements.phone.focus();
            return;
        }

        // 5b. Trùng email
        if (code === 'EMAIL_EXISTS' || (lowerMsg.includes('email') && !lowerMsg.includes('số điện thoại'))) {
            setFieldError(registerForm, 'email', 'Email này đã được sử dụng.');
            registerForm.elements.email.focus();
            return;
        }

        // 5c. Backend trả về thông báo gộp: "Số điện thoại hoặc Email này đã được đăng ký."
        if (lowerMsg.includes('số điện thoại') && lowerMsg.includes('email')) {
            if (!email) {
                // Người dùng không nhập email -> 100% là do số điện thoại đã tồn tại
                setFieldError(registerForm, 'phone', 'Số điện thoại này đã được sử dụng.');
                registerForm.elements.phone.focus();
            } else {
                // Người dùng nhập cả 2 -> đánh dấu trực quan cả 2 trường để người dùng kiểm tra
                setFieldError(registerForm, 'phone', 'Số điện thoại có thể đã được đăng ký.');
                setFieldError(registerForm, 'email', 'Email có thể đã được đăng ký.');
                showRegisterStatus('Số điện thoại hoặc Email này đã được đăng ký. Vui lòng kiểm tra lại.');
                registerForm.elements.phone.focus();
            }
            return;
        }

        // 5d. Lỗi mật khẩu từ Backend
        if (lowerMsg.includes('mật khẩu')) {
            setFieldError(registerForm, 'regPassword', msg);
            registerForm.elements.password.focus();
            return;
        }

        // 5e. Lỗi họ tên từ Backend
        if (lowerMsg.includes('họ') || lowerMsg.includes('tên')) {
            setFieldError(registerForm, 'fullName', msg);
            registerForm.elements.fullName.focus();
            return;
        }

        // Lỗi chung khác
        showRegisterStatus(msg || 'Đăng ký không thành công. Vui lòng thử lại.');
    } finally {
        isRegisterSubmitting = false;
        if (submitButton) {
            submitButton.disabled = false;
            submitButton.innerHTML = originalButtonContent;
        }
        updateRegisterButtonState();
    }
});

// ======================================================
// 12. XỬ LÝ QUÊN MẬT KHẨU
// ======================================================

if (forgotForm) {
    const forgotEmail = forgotForm.querySelector('#forgot-email');
    const forgotError = forgotForm.querySelector('#forgot-error');
    const forgotSubmitButton = forgotForm.querySelector('#btn-submit-forgot');

    if (forgotEmail && forgotError && forgotSubmitButton) {
        forgotEmail.addEventListener('input', () => {
            forgotError.textContent = '';
            forgotError.classList.add('hidden');
            setInputErrorState(forgotEmail, false);
        });

        forgotForm.addEventListener('submit', async (event) => {
            event.preventDefault();

            const email = forgotEmail.value.trim();

            forgotError.textContent = '';
            forgotError.classList.add('hidden');

            if (!email) {
                forgotError.textContent = 'Vui lòng nhập địa chỉ email.';
                forgotError.classList.remove('hidden');
                setInputErrorState(forgotEmail, true);
                forgotEmail.focus();
                return;
            }

            if (!getEmailPattern().test(email)) {
                forgotError.textContent = 'Vui lòng nhập một địa chỉ email hợp lệ.';
                forgotError.classList.remove('hidden');
                setInputErrorState(forgotEmail, true);
                forgotEmail.focus();
                return;
            }

            forgotSubmitButton.disabled = true;
            forgotSubmitButton.innerHTML = '<span class="material-symbols-outlined animate-spin text-lg">progress_activity</span><span>Đang gửi yêu cầu...</span>';

            try {
                await authApi.forgotPassword(email);

                // Tạo giao diện thông báo bằng DOM an toàn
                forgotForm.replaceChildren();

                const wrapper = document.createElement('div');
                wrapper.className = 'text-center py-6 space-y-3';

                const icon = document.createElement('span');
                icon.className = 'material-symbols-outlined text-tertiary text-5xl';
                icon.textContent = 'mark_email_read';

                const heading = document.createElement('h3');
                heading.className = 'font-bold text-lg text-on-surface';
                heading.textContent = 'Đã gửi yêu cầu khôi phục';

                const message = document.createElement('p');
                message.className = 'text-sm text-on-surface-variant';
                message.textContent = `Nếu email ${email} tồn tại trong hệ thống GustoPOS, hướng dẫn đặt lại mật khẩu sẽ được gửi đến hộp thư của bạn.`;

                const backButton = document.createElement('button');
                backButton.type = 'button';
                backButton.className = 'w-full h-12 mt-4 rounded-lg bg-surface-container-high font-semibold hover:bg-surface-variant transition-colors';
                backButton.textContent = 'Quay lại đăng nhập';

                backButton.addEventListener('click', () => {
                    switchTab('login');
                });

                wrapper.append(icon, heading, message, backButton);
                forgotForm.appendChild(wrapper);
            } catch (error) {
                forgotError.textContent = error?.message || 'Không thể gửi yêu cầu lúc này. Vui lòng thử lại sau.';
                forgotError.classList.remove('hidden');
                setInputErrorState(forgotEmail, true);
                forgotSubmitButton.disabled = false;
                forgotSubmitButton.textContent = 'Gửi liên kết khôi phục';
            }
        });
    }
}

// ======================================================
// 13. XỬ LÝ ĐĂNG NHẬP GOOGLE
// ======================================================

if (btnGoogleLogin) {
    btnGoogleLogin.addEventListener('click', () => {
        const googleAuthUrl = btnGoogleLogin.dataset.googleAuthUrl || `${API_BASE_URL}/auth/google/login`;

        let target;
        try {
            target = new URL(googleAuthUrl, location.href);
        } catch {
            showGlobalStatus('Đường dẫn đăng nhập Google không hợp lệ.');
            return;
        }

        const isLocalhost =
            target.hostname === 'localhost' ||
            target.hostname === '127.0.0.1' ||
            target.hostname === '[::1]';

        const isSameOrigin = target.origin === location.origin;
        const isSecure = target.protocol === 'https:';
        const isLocalDevelopment = isLocalhost && target.protocol === 'http:';

        if (!isSameOrigin && !isSecure && !isLocalDevelopment) {
            showGlobalStatus('Đường dẫn đăng nhập Google không an toàn.');
            return;
        }

        const returnUrl = getSafeReturnUrl();
        if (returnUrl && !returnUrl.includes('login_register')) {
            target.searchParams.set('returnUrl', returnUrl);
        }

        btnGoogleLogin.disabled = true;
        btnGoogleLogin.setAttribute('aria-busy', 'true');

        showGlobalStatus('Đang chuyển đến trang đăng nhập Google...');
        location.assign(target.href);
    });
}

// ======================================================
// 14. KHỞI TẠO TRẠNG THÁI BAN ĐẦU
// ======================================================

// Kiểm tra query parameter từ URL (ví dụ lỗi Google OAuth từ Backend redirect)
const pageParams = new URLSearchParams(window.location.search);
const oauthError = pageParams.get('error') || pageParams.get('oauthError');
if (oauthError) {
    let msg = 'Đăng nhập bằng tài khoản Google không thành công. Vui lòng thử lại.';
    if (oauthError === 'access_denied') {
        msg = 'Bạn đã hủy yêu cầu đăng nhập bằng tài khoản Google.';
    } else if (oauthError === 'invalid_client') {
        msg = 'Cấu hình Google Client Secret trên máy chủ chưa chính xác hoặc chưa được thiết lập. Vui lòng kiểm tra lại cấu hình.';
    } else if (oauthError === 'correlation_failed') {
        msg = 'Phiên bảo mật OAuth tạm thời bị gián đoạn. Vui lòng thử lại.';
    } else if (oauthError === 'no_email') {
        msg = 'Tài khoản Google của bạn không cung cấp địa chỉ email để định danh.';
    } else if (oauthError === 'auth_failed') {
        msg = 'Không tìm thấy phiên xác thực Google tạm thời. Vui lòng thử lại.';
    }
    showGlobalStatus(msg);
}

clearRegisterErrors();
updateRegisterButtonState();
setupRememberMeListeners();
restoreRememberedLogin();

