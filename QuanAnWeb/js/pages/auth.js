import { authApi } from '../api/authApi.js?auth-session-2';
import {
    clearCustomerSession,
    saveCustomerSession,
    setCurrentCustomer
} from '../api/session.js?auth-session-2';
import { profileApi } from '../api/profileApi.js?auth-session-2';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';

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

if (!loginTab || !registerTab || !loginForm || !registerForm) {
    throw new Error('Không tìm thấy form đăng nhập hoặc đăng ký trên trang.');
}

setupSessionNavigation();

// ======================================================
// 2. CÁC HÀM HỖ TRỢ
// ======================================================

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

function getSafeReturnUrl() {
    const requested = new URLSearchParams(location.search).get('returnUrl');

    if (!requested) {
        return '../profile/index.html';
    }

    try {
        const target = new URL(requested, location.href);

        // Chỉ chuyển đến trang cùng website để tránh open redirect.
        if (target.origin !== location.origin) {
            return '../profile/index.html';
        }

        return `${target.pathname}${target.search}${target.hash}`;
    } catch {
        return '../profile/index.html';
    }
}

function setInputErrorState(input, isError) {
    if (!input) return;

    input.classList.toggle('border-error', isError);
    input.classList.toggle('border-outline-variant', !isError);
    input.classList.toggle('focus:border-error', isError);
    input.classList.toggle('focus:border-primary', !isError);
}

function setFieldError(form, fieldKey, message) {
    const errorNode = form.querySelector(`[data-error="${fieldKey}"]`);

    if (errorNode) {
        errorNode.textContent = message || '';
    }

    const input = getInputForField(form, fieldKey);

    if (input) {
        setInputErrorState(input, Boolean(message));
        input.setAttribute('aria-invalid', String(Boolean(message)));
    }
}

function getEmailPattern() {
    return /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+$/;
}

function showGlobalStatus(message) {
    if (globalStatus) {
        globalStatus.textContent = message || '';
    }
}

function showRegisterStatus(message) {
    if (registerStatus) {
        registerStatus.textContent = message || '';
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

    // Nếu không phải email và không phải chuỗi số,
    // xem đây là tên tài khoản, ví dụ admin, nhanvien01.
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
        !/[A-Za-z]/.test(value) ||
        !/\d/.test(value)
    ) {
        return 'Mật khẩu phải có ít nhất 8 ký tự, bao gồm 1 chữ cái và 1 chữ số.';
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
        return 'Vui lòng đồng ý với điều khoản.';
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
    const fullName = registerForm.elements.fullName.value.trim();
    const phone = registerForm.elements.phone.value.trim();
    const email = registerForm.elements.email.value.trim();
    const password = registerForm.elements.password.value;
    const confirmPassword = registerForm.elements.confirmPassword.value;
    const termsAccepted = registerForm.elements.terms.checked;

    const isValid =
        !validateFullName(fullName) &&
        !validateRegisterPhone(phone) &&
        !validateRegisterEmail(email) &&
        !validateRegisterPassword(password) &&
        !validateConfirmPassword(password, confirmPassword) &&
        termsAccepted;

    if (registerButton) {
        registerButton.disabled = !isValid;
        registerButton.classList.toggle('opacity-50', !isValid);
        registerButton.classList.toggle('cursor-not-allowed', !isValid);
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

    const identifier = loginForm.elements.login.value.trim();
    const password = loginForm.elements.password.value;

    const identifierError = validateLoginIdentifier(identifier);
    const passwordError = validateLoginPassword(password);

    setFieldError(loginForm, 'login', identifierError);
    setFieldError(loginForm, 'password', passwordError);

    if (identifierError || passwordError) {
        return;
    }

    const submitButton = loginForm.querySelector(
        'button[type="submit"]'
    );

    const originalButtonText = submitButton
        ? submitButton.textContent
        : 'Đăng nhập';

    if (submitButton) {
        submitButton.disabled = true;
        submitButton.textContent = 'Đang đăng nhập...';
    }

    showGlobalStatus('');

    try {
        // Gửi tên đăng nhập, email hoặc số điện thoại cùng mật khẩu.
        const response = await authApi.login(identifier, password);

        // Lưu phiên đăng nhập do backend trả về.
        saveCustomerSession(response);

        // Lấy thông tin tài khoản đang đăng nhập.
        const customer = await profileApi.getMyProfile();

        setCurrentCustomer(customer);

        globalThis.dispatchEvent(
            new CustomEvent('gusto:session-updated')
        );

        // Chuyển đến trang tiếp theo sau khi đăng nhập thành công.
        location.assign(getSafeReturnUrl());
    } catch (error) {
        clearCustomerSession();

        showGlobalStatus(
            error?.message ||
            'Tài khoản hoặc mật khẩu không chính xác. Vui lòng thử lại.'
        );
    } finally {
        if (submitButton) {
            submitButton.disabled = false;
            submitButton.textContent = originalButtonText;
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
            setFieldError(
                registerForm,
                'fullName',
                validateFullName(registerForm.elements.fullName.value)
            );
        }

        if (fieldKey === 'phone') {
            setFieldError(
                registerForm,
                'phone',
                validateRegisterPhone(registerForm.elements.phone.value)
            );
        }

        if (fieldKey === 'email') {
            setFieldError(
                registerForm,
                'email',
                validateRegisterEmail(registerForm.elements.email.value)
            );
        }

        if (fieldKey === 'password') {
            setFieldError(
                registerForm,
                'regPassword',
                validateRegisterPassword(registerForm.elements.password.value)
            );

            if (registerForm.elements.confirmPassword.value) {
                setFieldError(
                    registerForm,
                    'confirmPassword',
                    validateConfirmPassword(
                        registerForm.elements.password.value,
                        registerForm.elements.confirmPassword.value
                    )
                );
            }
        }

        if (fieldKey === 'confirmPassword') {
            setFieldError(
                registerForm,
                'confirmPassword',
                validateConfirmPassword(
                    registerForm.elements.password.value,
                    registerForm.elements.confirmPassword.value
                )
            );
        }

        updateRegisterButtonState();
        showRegisterStatus('');
    });

    input.addEventListener('blur', () => {
        if (fieldKey === 'fullName') {
            setFieldError(
                registerForm,
                'fullName',
                validateFullName(registerForm.elements.fullName.value)
            );
        }

        if (fieldKey === 'phone') {
            setFieldError(
                registerForm,
                'phone',
                validateRegisterPhone(registerForm.elements.phone.value)
            );
        }

        if (fieldKey === 'email') {
            setFieldError(
                registerForm,
                'email',
                validateRegisterEmail(registerForm.elements.email.value)
            );
        }

        if (fieldKey === 'password') {
            setFieldError(
                registerForm,
                'regPassword',
                validateRegisterPassword(registerForm.elements.password.value)
            );
        }

        if (fieldKey === 'confirmPassword') {
            setFieldError(
                registerForm,
                'confirmPassword',
                validateConfirmPassword(
                    registerForm.elements.password.value,
                    registerForm.elements.confirmPassword.value
                )
            );
        }

        updateRegisterButtonState();
    });
});

registerForm.elements.terms.addEventListener('change', () => {
    setFieldError(
        registerForm,
        'terms',
        validateTerms(registerForm.elements.terms.checked)
    );

    updateRegisterButtonState();
    showRegisterStatus('');
});

// ======================================================
// 11. XỬ LÝ ĐĂNG KÝ
// ======================================================

registerForm.addEventListener('submit', async (event) => {
    event.preventDefault();

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

    if (
        fullNameError ||
        phoneError ||
        emailError ||
        passwordError ||
        confirmError ||
        termsError
    ) {
        updateRegisterButtonState();
        return;
    }

    const submitButton = registerForm.querySelector(
        'button[type="submit"]'
    );

    const originalButtonText = submitButton
        ? submitButton.textContent
        : 'Tạo tài khoản';

    if (submitButton) {
        submitButton.disabled = true;
        submitButton.textContent = 'Đang tạo tài khoản...';
    }

    showRegisterStatus('');

    try {
        await authApi.register({
            fullName,
            phone,
            email: email || null,
            password
        });

        showGlobalStatus(
            'Tài khoản đã được tạo. Hãy đăng nhập để tiếp tục.'
        );

        switchTab('login');
    } catch (error) {
        showRegisterStatus(
            error?.message ||
            'Đăng ký không thành công. Vui lòng thử lại.'
        );
    } finally {
        if (submitButton) {
            submitButton.disabled = false;
            submitButton.textContent = originalButtonText;
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
    const forgotSubmitButton = forgotForm.querySelector(
        '#btn-submit-forgot'
    );

    if (forgotEmail && forgotError && forgotSubmitButton) {
        forgotForm.addEventListener('submit', async (event) => {
            event.preventDefault();

            const email = forgotEmail.value.trim();

            forgotError.textContent = '';
            forgotError.classList.add('hidden');

            if (!getEmailPattern().test(email)) {
                forgotError.textContent =
                    'Vui lòng nhập một địa chỉ email hợp lệ.';

                forgotError.classList.remove('hidden');
                return;
            }

            forgotSubmitButton.disabled = true;
            forgotSubmitButton.textContent = 'Đang gửi yêu cầu...';

            try {
                await authApi.forgotPassword(email);

                // Tạo giao diện thông báo bằng DOM,
                // không đưa trực tiếp dữ liệu người dùng vào innerHTML.
                forgotForm.replaceChildren();

                const wrapper = document.createElement('div');
                wrapper.className = 'text-center py-6 space-y-3';

                const icon = document.createElement('span');
                icon.className =
                    'material-symbols-outlined text-tertiary text-5xl';
                icon.textContent = 'mark_email_read';

                const heading = document.createElement('h3');
                heading.className = 'font-bold text-lg text-on-surface';
                heading.textContent = 'Đã gửi yêu cầu khôi phục';

                const message = document.createElement('p');
                message.className =
                    'text-sm text-on-surface-variant';
                message.textContent =
                    `Nếu email ${email} tồn tại trong hệ thống, ` +
                    'bạn sẽ nhận được hướng dẫn khôi phục mật khẩu. ' +
                    'Vui lòng kiểm tra hộp thư.';

                const backButton = document.createElement('button');
                backButton.type = 'button';
                backButton.className =
                    'w-full h-12 mt-4 rounded-lg ' +
                    'bg-surface-container-high font-semibold ' +
                    'hover:bg-surface-variant transition-colors';
                backButton.textContent = 'Quay lại đăng nhập';

                backButton.addEventListener('click', () => {
                    location.reload();
                });

                wrapper.append(
                    icon,
                    heading,
                    message,
                    backButton
                );

                forgotForm.appendChild(wrapper);
            } catch (error) {
                forgotError.textContent =
                    error?.message ||
                    'Không thể gửi yêu cầu. Vui lòng thử lại sau.';

                forgotError.classList.remove('hidden');

                forgotSubmitButton.disabled = false;
                forgotSubmitButton.textContent =
                    'Gửi yêu cầu khôi phục';
            }
        });
    }
}

// ======================================================
// 13. XỬ LÝ ĐĂNG NHẬP GOOGLE
// ======================================================

// Nút HTML cần có data-google-auth-url trỏ đến endpoint OAuth
// do backend cung cấp.
//
// Ví dụ khi backend chạy tại localhost:5000:
// data-google-auth-url="http://localhost:5000/auth/google/login"
//
// Endpoint này phải thực sự được backend triển khai.
// Frontend không thể tự xác thực Google chỉ bằng JavaScript.

if (btnGoogleLogin) {
    btnGoogleLogin.addEventListener('click', () => {
        const googleAuthUrl = btnGoogleLogin.dataset.googleAuthUrl;

        if (!googleAuthUrl) {
            showGlobalStatus(
                'Đăng nhập Google chưa được cấu hình. ' +
                'Vui lòng cấu hình endpoint OAuth của backend.'
            );

            return;
        }

        let target;

        try {
            target = new URL(googleAuthUrl, location.href);
        } catch {
            showGlobalStatus(
                'Đường dẫn đăng nhập Google không hợp lệ.'
            );

            return;
        }

        // Chỉ cho phép HTTPS, cùng origin hoặc HTTP localhost để phát triển.
        const isLocalhost =
            target.hostname === 'localhost' ||
            target.hostname === '127.0.0.1' ||
            target.hostname === '[::1]';

        const isSameOrigin = target.origin === location.origin;
        const isSecure = target.protocol === 'https:';
        const isLocalDevelopment =
            isLocalhost && target.protocol === 'http:';

        if (!isSameOrigin && !isSecure && !isLocalDevelopment) {
            showGlobalStatus(
                'Đường dẫn đăng nhập Google không an toàn.'
            );

            return;
        }

        btnGoogleLogin.disabled = true;
        btnGoogleLogin.setAttribute('aria-busy', 'true');

        showGlobalStatus('Đang chuyển đến trang đăng nhập Google...');

        // Backend sẽ điều hướng sang Google, xử lý callback,
        // xác thực tài khoản và tạo phiên đăng nhập.
        location.assign(target.href);
    });
}

// ======================================================
// 14. KHỞI TẠO TRẠNG THÁI BAN ĐẦU
// ======================================================

clearRegisterErrors();
updateRegisterButtonState();