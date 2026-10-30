import { authApi } from '../api/authApi.js?auth-session-2';
import { clearCustomerSession } from '../api/session.js?auth-session-2';

// 0. TRÍCH XUẤT TOKEN NGAY KHI MODULE ĐƯỢC LOAD (TRÁNH BỊ MẤT DO BẤT KỲ EVENT NÀO TRƯỚC ĐÓ)
function extractResetToken() {
    try {
        if (typeof window === 'undefined') return null;

        const searchParams = new URLSearchParams(window.location.search);
        const searchToken = searchParams.get('token') || searchParams.get('code');
        if (typeof searchToken === 'string' && searchToken.trim()) {
            return searchToken.trim();
        }

        const hash = window.location.hash || '';
        if (hash.includes('token=')) {
            const hashQuery = hash.includes('?') ? hash.substring(hash.indexOf('?')) : hash.replace(/^#\/?/, '?');
            const hashParams = new URLSearchParams(hashQuery);
            const hashToken = hashParams.get('token') || hashParams.get('code');
            if (typeof hashToken === 'string' && hashToken.trim()) {
                return hashToken.trim();
            }
        }
    } catch {
        // Bỏ qua lỗi cú pháp URL
    }
    return null;
}

const initialCapturedToken = extractResetToken();

document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('form-reset-password');
    const newPasswordInput = document.getElementById('new-password');
    const confirmPasswordInput = document.getElementById('confirm-password');
    const submitBtn = document.getElementById('btn-submit-reset');
    const successBox = document.getElementById('reset-success');
    const globalStatus = document.querySelector('[data-reset-status]');

    // 1. CÁC HÀM TIỆN ÍCH XỬ LÝ LỖI GIAO DIỆN
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

    function setFieldError(fieldKey, message) {
        const errorNode = form.querySelector(`[data-error="${fieldKey}"]`);
        if (errorNode) {
            errorNode.textContent = message || '';
        }
        const input = document.getElementById(fieldKey);
        if (input) {
            setInputErrorState(input, Boolean(message));
        }
    }

    // 2. CÁC HÀM VALIDATE THEO CHÍNH SÁCH BACKEND (8-128 ký tự, ít nhất 1 chữ cái, 1 chữ số)
    function validatePassword(value) {
        if (!value) return 'Vui lòng nhập mật khẩu mới.';
        if (value.length < 8 || value.length > 128 || !/[A-Za-z]/.test(value) || !/\d/.test(value)) {
            return 'Mật khẩu phải có từ 8 đến 128 ký tự, gồm ít nhất một chữ cái và một chữ số.';
        }
        return '';
    }

    function validateConfirmPassword(password, confirmPassword) {
        if (!confirmPassword) return 'Vui lòng xác nhận mật khẩu.';
        if (password !== confirmPassword) return 'Mật khẩu xác nhận không trùng khớp.';
        return '';
    }

    // 3. XỬ LÝ NÚT ẨN/HIỆN MẬT KHẨU
    document.querySelectorAll('[data-password-toggle]').forEach((button) => {
        button.addEventListener('click', () => {
            const input = document.getElementById(button.dataset.passwordToggle);
            if (!input) return;
            const nextType = input.type === 'password' ? 'text' : 'password';
            input.type = nextType;
            const visible = nextType === 'text';
            button.setAttribute('aria-label', visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
            const icon = button.querySelector('.material-symbols-outlined');
            if (icon) {
                icon.textContent = visible ? 'visibility_off' : 'visibility';
            }
        });
    });

    // 4. LẮNG NGHE SỰ KIỆN NHẬP LIỆU
    newPasswordInput.addEventListener('input', () => {
        if (newPasswordInput.value) {
            const err = validatePassword(newPasswordInput.value);
            setFieldError('new-password', err);
        } else {
            setFieldError('new-password', '');
        }

        if (confirmPasswordInput.value) {
            setFieldError('confirm-password', validateConfirmPassword(newPasswordInput.value, confirmPasswordInput.value));
        }
        if (globalStatus) globalStatus.textContent = '';
    });

    newPasswordInput.addEventListener('blur', () => {
        setFieldError('new-password', validatePassword(newPasswordInput.value));
    });

    confirmPasswordInput.addEventListener('input', () => {
        if (confirmPasswordInput.value) {
            setFieldError('confirm-password', validateConfirmPassword(newPasswordInput.value, confirmPasswordInput.value));
        } else {
            setFieldError('confirm-password', '');
        }
        if (globalStatus) globalStatus.textContent = '';
    });

    confirmPasswordInput.addEventListener('blur', () => {
        setFieldError('confirm-password', validateConfirmPassword(newPasswordInput.value, confirmPasswordInput.value));
    });

    // 5. KIỂM TRA TOKEN TRÊN URL HOẶC ĐÃ CAPTURE
    const token = initialCapturedToken || extractResetToken();

    if (!token) {
        if (globalStatus) {
            globalStatus.innerHTML = `
                <span>Liên kết không hợp lệ hoặc thiếu mã xác thực.</span><br>
                <a href="./login_register.html#form-forgot-password" class="text-primary font-semibold underline mt-2 inline-block">
                    Quay lại trang Quên mật khẩu để lấy liên kết mới
                </a>
            `;
        }
        submitBtn.disabled = true;
        newPasswordInput.disabled = true;
        confirmPasswordInput.disabled = true;
        return;
    }

    submitBtn.disabled = false;

    // 6. XỬ LÝ SUBMIT FORM ĐẶT LẠI MẬT KHẨU
    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const newPassword = newPasswordInput.value;
        const confirmPassword = confirmPasswordInput.value;

        const err1 = validatePassword(newPassword);
        const err2 = validateConfirmPassword(newPassword, confirmPassword);

        setFieldError('new-password', err1);
        setFieldError('confirm-password', err2);

        if (err1) {
            newPasswordInput.focus();
            return;
        }
        if (err2) {
            confirmPasswordInput.focus();
            return;
        }

        submitBtn.disabled = true;
        const originalButtonHtml = submitBtn.innerHTML;
        submitBtn.textContent = 'Đang cập nhật...';
        if (globalStatus) globalStatus.textContent = '';

        try {
            await authApi.resetPassword(token, newPassword);

            // Dọn dẹp session cũ nếu có
            clearCustomerSession();

            form.classList.add('hidden');
            successBox.classList.remove('hidden');

        } catch (error) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalButtonHtml;

            let errorMessage = error?.message || 'Không thể đổi mật khẩu. Liên kết có thể đã hết hạn hoặc không hợp lệ.';
            const lowerMsg = errorMessage.toLowerCase();

            // Nếu lỗi liên quan đến mật khẩu -> hiển thị ngay dưới ô new-password
            if (lowerMsg.includes('mật khẩu') && (lowerMsg.includes('ký tự') || lowerMsg.includes('chữ') || lowerMsg.includes('dài') || lowerMsg.includes('yếu'))) {
                setFieldError('new-password', errorMessage);
                newPasswordInput.focus();
                return;
            }

            // Nếu lỗi do liên kết không hợp lệ, hết hạn hoặc đã được sử dụng
            if (lowerMsg.includes('liên kết') || lowerMsg.includes('hết hạn') || lowerMsg.includes('token') || lowerMsg.includes('sử dụng')) {
                if (globalStatus) {
                    globalStatus.innerHTML = `
                        <span>${errorMessage}</span><br>
                        <a href="./login_register.html#form-forgot-password" class="text-primary font-semibold underline mt-2 inline-block">
                            Yêu cầu gửi lại liên kết mới
                        </a>
                    `;
                }
                submitBtn.disabled = true;
                newPasswordInput.disabled = true;
                confirmPasswordInput.disabled = true;
                return;
            }

            // Lỗi mạng hoặc lỗi máy chủ khác
            if (globalStatus) globalStatus.textContent = errorMessage;
        }
    });
});
