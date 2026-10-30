import { getCurrentCustomer, hasCustomerSession } from '../api/session.js?auth-session-2';
import { ensureCustomerSession, logoutCustomerSession } from '../api/authSession.js?auth-session-2';
import { setupNotificationBell } from './notificationBell.js';

export function setupMobileMenu() {
    const toggleButtons = document.querySelectorAll('[data-mobile-menu-toggle]');
    const mobileMenus = document.querySelectorAll('[data-mobile-menu]');
    if (!toggleButtons.length && !mobileMenus.length) return;

    const setMenuOpen = (menu, button, open) => {
        if (!menu) return;
        menu.classList.toggle('hidden', !open);
        if (button) {
            button.setAttribute('aria-expanded', String(open));
            button.setAttribute('aria-label', open ? 'Đóng điều hướng' : 'Mở điều hướng');
            const icon = button.querySelector('.material-symbols-outlined');
            if (icon) {
                icon.textContent = open ? 'close' : 'menu';
            }
        }
    };

    toggleButtons.forEach((btn) => {
        if (btn.dataset.mobileMenuReady === 'true') return;
        btn.dataset.mobileMenuReady = 'true';

        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const targetId = btn.getAttribute('aria-controls');
            const menu = targetId ? document.getElementById(targetId) : document.querySelector('[data-mobile-menu]');
            if (!menu) return;
            const isOpen = !menu.classList.contains('hidden');
            setMenuOpen(menu, btn, !isOpen);
        });
    });

    mobileMenus.forEach((menu) => {
        if (menu.dataset.mobileMenuReady === 'true') return;
        menu.dataset.mobileMenuReady = 'true';

        menu.addEventListener('click', (e) => {
            if (e.target.closest('a') || e.target.closest('button[data-customer-logout]')) {
                const targetId = menu.id;
                const button = (targetId && document.querySelector(`[data-mobile-menu-toggle][aria-controls="${targetId}"]`)) ||
                               document.querySelector('[data-mobile-menu-toggle]');
                setMenuOpen(menu, button, false);
            }
        });
    });

    if (document.documentElement.dataset.mobileMenuGlobalReady !== 'true') {
        document.documentElement.dataset.mobileMenuGlobalReady = 'true';

        document.addEventListener('click', (e) => {
            if (!e.target.closest('[data-mobile-menu]') && !e.target.closest('[data-mobile-menu-toggle]')) {
                document.querySelectorAll('[data-mobile-menu]').forEach((m) => {
                    const targetId = m.id;
                    const btn = (targetId && document.querySelector(`[data-mobile-menu-toggle][aria-controls="${targetId}"]`)) ||
                                document.querySelector('[data-mobile-menu-toggle]');
                    setMenuOpen(m, btn, false);
                });
            }
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                document.querySelectorAll('[data-mobile-menu]').forEach((m) => {
                    const targetId = m.id;
                    const btn = (targetId && document.querySelector(`[data-mobile-menu-toggle][aria-controls="${targetId}"]`)) ||
                                document.querySelector('[data-mobile-menu-toggle]');
                    setMenuOpen(m, btn, false);
                });
            }
        });
    }
}

export function setupSessionNavigation() {
    const initBell = () => {
        try {
            setupNotificationBell();
        } catch (err) {
            console.warn('[NotificationBell] Init error:', err);
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initBell, { once: true });
    } else {
        initBell();
    }

    setupMobileMenu();

    const refresh = async (restore = true) => {
        if (restore && (!hasCustomerSession() || !getCurrentCustomer())) {
            await ensureCustomerSession();
        }
        const authenticated = hasCustomerSession();
        const customer = getCurrentCustomer();
        const name = customer?.fullName ?? customer?.FullName ?? (authenticated ? 'Tài khoản' : 'Đăng nhập');

        initBell();
        setupMobileMenu();

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
        globalThis.addEventListener('gusto:session-updated', () => { refresh(true); });
    }
}
