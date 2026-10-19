import { apiClient } from '../api/client.js?auth-session-2';

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

    // 2. CÁC HÀM VALIDATE
    function validatePassword(value) {
        if (!value) return 'Vui lòng nhập mật khẩu mới.';
        if (value.length < 8 || !/[A-Za-z]/.test(value) || !/\d/.test(value)) {
            return 'Mật khẩu phải có ít nhất 8 ký tự, bao gồm 1 chữ cái và 1 chữ số.';
        }
        return '';
    }

    function validateConfirmPassword(password, confirmPassword) {
        if (!confirmPassword) return 'Vui lòng xác nhận mật khẩu.';
        if (password !== confirmPassword) return 'Mật khẩu xác nhận không trùng khớp.';
        return '';
    }

    function updateButtonState() {
        const p1 = newPasswordInput.value;
        const p2 = confirmPasswordInput.value;
        const isValid = !validatePassword(p1) && !validateConfirmPassword(p1, p2);

        submitBtn.disabled = !isValid;
        submitBtn.classList.toggle('opacity-50', !isValid);
        submitBtn.classList.toggle('cursor-not-allowed', !isValid);
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
        setFieldError('new-password', validatePassword(newPasswordInput.value));
        if (confirmPasswordInput.value) {
            setFieldError('confirm-password', validateConfirmPassword(newPasswordInput.value, confirmPasswordInput.value));
        }
        updateButtonState();
        if (globalStatus) globalStatus.textContent = '';
    });

    newPasswordInput.addEventListener('blur', () => {
        setFieldError('new-password', validatePassword(newPasswordInput.value));
        updateButtonState();
    });

    confirmPasswordInput.addEventListener('input', () => {
        setFieldError('confirm-password', validateConfirmPassword(newPasswordInput.value, confirmPasswordInput.value));
        updateButtonState();
        if (globalStatus) globalStatus.textContent = '';
    });

    confirmPasswordInput.addEventListener('blur', () => {
        setFieldError('confirm-password', validateConfirmPassword(newPasswordInput.value, confirmPasswordInput.value));
        updateButtonState();
    });

    // 5. KIỂM TRA TOKEN TRÊN URL
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');

    if (!token) {
        if (globalStatus) globalStatus.textContent = 'Liên kết không hợp lệ hoặc không tìm thấy mã xác thực.';
        submitBtn.disabled = true;
        newPasswordInput.disabled = true;
        confirmPasswordInput.disabled = true;
        return;
    }

    updateButtonState();

    // 6. XỬ LÝ SUBMIT FORM
    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const newPassword = newPasswordInput.value;
        const confirmPassword = confirmPasswordInput.value;

        const err1 = validatePassword(newPassword);
        const err2 = validateConfirmPassword(newPassword, confirmPassword);

        setFieldError('new-password', err1);
        setFieldError('confirm-password', err2);

        if (err1 || err2) return;

        submitBtn.disabled = true;
        submitBtn.textContent = 'Đang cập nhật...';
        if (globalStatus) globalStatus.textContent = '';

        try {
            await apiClient.post('/auth/reset-password', { 
                token: token, 
                newPassword: newPassword 
            });

            form.classList.add('hidden');
            successBox.classList.remove('hidden');

        } catch (error) {
            // Đã đồng bộ: Bắt trọn vẹn lỗi cấu trúc từ ASP.NET Core (ValidationErrors)
            let errorMessage = 'Không thể đổi mật khẩu. Liên kết có thể đã hết hạn hoặc không hợp lệ.';
            
            if (error) {
                // Kiểm tra xem lỗi có chứa thuộc tính dạng mảng validation errors hay không
                if (error.errors && typeof error.errors === 'object') {
                    const firstErrorKey = Object.keys(error.errors)[0];
                    if (firstErrorKey && error.errors[firstErrorKey].length > 0) {
                        errorMessage = error.errors[firstErrorKey][0];
                    }
                } else if (error.title) {
                    errorMessage = error.title;
                } else if (error.message) {
                    errorMessage = error.message;
                }
            }

            if (globalStatus) globalStatus.textContent = errorMessage;
            submitBtn.disabled = false;
            submitBtn.textContent = 'Cập nhật mật khẩu';
        }
    });
});