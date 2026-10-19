import { getCurrentCustomer, hasCustomerSession } from '../api/session.js?auth-session-2';
import { ensureCustomerSession, logoutCustomerSession } from '../api/authSession.js?auth-session-2';

export function setupSessionNavigation() {
    const refresh = async (restore = true) => {
        if (restore && !hasCustomerSession()) await ensureCustomerSession();
        const authenticated = hasCustomerSession();
        const customer = getCurrentCustomer();
        const name = customer?.fullName ?? customer?.FullName ?? (authenticated ? 'Tài khoản' : 'Đăng nhập');

        document.querySelectorAll('[data-customer-account]').forEach((link) => {
            link.href = authenticated ? link.dataset.profileHref : link.dataset.loginHref;
            link.setAttribute('aria-label', authenticated ? `Hồ sơ ${name}` : 'Đăng nhập');
            const label = link.querySelector('[data-customer-link-label]');
            if (label) label.textContent = authenticated ? 'Hồ sơ' : 'Đăng nhập';
        });
        document.querySelectorAll('[data-customer-name], #header-user-name, #home-user-name').forEach((element) => {
            element.textContent = name;
        });

        document.querySelectorAll('[data-customer-logout]').forEach((button) => {
            button.classList.toggle('hidden', !authenticated);
            if (button.dataset.logoutReady === 'true') return;
            button.dataset.logoutReady = 'true';
            button.addEventListener('click', async () => {
                button.disabled = true;
                const originalText = button.textContent;
                try {
                    await logoutCustomerSession();
                } catch {
                    button.textContent = 'Không thể đăng xuất. Thử lại.';
                    button.disabled = false;
                    return;
                }
                location.assign(new URL('/index.html', location.origin));
                button.textContent = originalText;
            });
        });
    };

    refresh();
    if (document.documentElement.dataset.sessionNavigationReady !== 'true') {
        document.documentElement.dataset.sessionNavigationReady = 'true';
        globalThis.addEventListener('gusto:unauthorized', () => { refresh(false); });
        globalThis.addEventListener('gusto:session-updated', () => { refresh(false); });
    }
}
