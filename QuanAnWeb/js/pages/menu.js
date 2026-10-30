import { menuApi } from '../api/menuApi.js?auth-session-2';
import { renderCartDrawer } from '../components/cartView.js';
import { readCart, writeCart } from '../components/cartStore.js';
import { setupSessionNavigation } from '../components/sessionNavigation.js?auth-session-2';
import { realtimeClient } from '../realtime/realtimeClient.js';
import { 
    renderCategories, 
    renderDishes, 
    renderEmpty, 
    renderError, 
    renderLoading, 
    renderDishDetailModal, 
    updateModalIngredients, 
    showToast 
} from '../components/menuView.js';

function initializeMenuPage() {
    if (document.documentElement.dataset.menuPageInitialized === 'true') return;
    document.documentElement.dataset.menuPageInitialized = 'true';
    setupSessionNavigation();

    const categoriesElement = document.querySelector('[data-menu-categories]');
    const dishesElement = document.querySelector('[data-menu-dishes]');
    const cartRoot = document.querySelector('[data-cart-root]');
    const cartButton = document.querySelector('[data-cart-open]');
    const cartCount = document.querySelector('[data-cart-count]');
    const mobileMenuButton = document.querySelector('[data-mobile-menu-toggle]');
    const mobileMenu = document.querySelector('[data-mobile-menu]');
    const searchInputs = [...document.querySelectorAll('[data-menu-search]')];
    if (!categoriesElement || !dishesElement || !cartRoot || !cartButton || !cartCount) return;

    const errorMessage = 'Không thể tải thực đơn. Vui lòng thử lại.';
    let categories = [];
    let dishes = [];
    let dishesLoaded = false;
    let selectedCategoryId = '';
    let requestVersion = 0;
    let cartItems = readCart();
    let cartIsOpen = false;

    // Phần tử gốc chứa Modal chi tiết món ăn
    let detailRoot = document.querySelector('[data-dish-detail-root]');
    if (!detailRoot) {
        detailRoot = document.createElement('div');
        detailRoot.setAttribute('data-dish-detail-root', '');
        detailRoot.className = 'fixed inset-0 z-[60] hidden pointer-events-none';
        document.body.append(detailRoot);
    }

    let activeDishDetailId = null;
    let lastFocusedElement = null;

    function closeDishDetail() {
        if (!activeDishDetailId && detailRoot.classList.contains('hidden')) return;
        activeDishDetailId = null;
        detailRoot.classList.add('hidden', 'pointer-events-none');
        detailRoot.replaceChildren();
        document.body.classList.remove('overflow-hidden');
        if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
            lastFocusedElement.focus();
            lastFocusedElement = null;
        }
    }

    function setCartOpen(isOpen) {
        cartIsOpen = isOpen;
        cartRoot.querySelector('[data-cart-layer]')?.classList.toggle('hidden', !cartIsOpen);
        cartButton.setAttribute('aria-expanded', String(cartIsOpen));
        document.body.classList.toggle('overflow-hidden', cartIsOpen || Boolean(activeDishDetailId));
        if (cartIsOpen) cartRoot.querySelector('[data-cart-close]')?.focus();
        else cartButton.focus();
    }

    async function showDishDetail(dish) {
        if (!dish) return;
        const dishId = dish.id;
        activeDishDetailId = dishId;
        lastFocusedElement = document.activeElement;

        // Render modal hiển thị tức thì thông tin món
        renderDishDetailModal(detailRoot, dish, {
            onClose: closeDishDetail,
            onAddToCart: (dishToAdd) => {
                addToCart(dishToAdd);
            }
        });

        detailRoot.classList.remove('hidden', 'pointer-events-none');
        document.body.classList.add('overflow-hidden');

        // Focus vào nút đóng để hỗ trợ thao tác bàn phím
        detailRoot.querySelector('[data-dish-detail-close]')?.focus();

        // Nếu món đã có sẵn danh sách ingredients
        if (Array.isArray(dish.ingredients) && dish.ingredients.length > 0) {
            updateModalIngredients(detailRoot, dish.ingredients);
            return;
        }

        // Gọi backend /api/menu/{id} để lấy nguyên liệu thật từ database (MenuItemIngredients)
        try {
            const fullDetail = await menuApi.getDishDetail(dishId);
            if (activeDishDetailId === dishId) {
                dish.ingredients = fullDetail.ingredients;
                updateModalIngredients(detailRoot, fullDetail.ingredients);
            }
        } catch (err) {
            console.warn('Không thể tải chi tiết món từ backend:', dishId, err);
            if (activeDishDetailId === dishId) {
                updateModalIngredients(detailRoot, []);
            }
        }
    }

    function updateCartView() {
        renderCartDrawer(
            cartRoot,
            cartItems,
            updateCartQuantity,
            removeFromCart,
            updateCartNote,
            () => setCartOpen(false)
        );
        cartCount.textContent = String(cartItems.reduce((sum, item) => sum + item.quantity, 0));
        cartCount.classList.toggle('hidden', cartItems.length === 0);
        setCartOpen(cartIsOpen);
    }

    function addToCart(dish) {
        // Đã sửa: Chuẩn hóa kiểm tra dữ liệu và trạng thái isAvailable
        const dishId = dish?.id ?? dish?.Id;
        const name = dish?.name ?? dish?.Name;
        const price = Number(dish?.price ?? dish?.Price);
        const isAvailable = Boolean(dish?.isAvailable ?? dish?.IsAvailable ?? true);

        if (dishId == null || typeof name !== 'string' || !Number.isFinite(price) || price < 0 || !isAvailable) {
            return; // Chặn thêm vào giỏ nếu dữ liệu lỗi hoặc món đã hết
        }

        const itemId = String(dishId);
        const existingItem = cartItems.find((item) => item.id === itemId);
        if (existingItem) existingItem.quantity = Math.min(existingItem.quantity + 1, 99);
        else cartItems.push({ id: itemId, name, price, quantity: 1, note: '' });
        
        writeCart(cartItems);
        updateCartView();
        showToast(`Đã thêm "${name}" vào giỏ hàng!`, 'success');
    }

    function updateCartQuantity(itemId, quantity) {
        quantity = Math.min(quantity, 99);
        if (quantity <= 0) {
            removeFromCart(itemId);
            return;
        }
        const item = cartItems.find((cartItem) => cartItem.id === String(itemId));
        if (!item) return;
        item.quantity = quantity;
        writeCart(cartItems);
        updateCartView();
    }

    function removeFromCart(itemId) {
        cartItems = cartItems.filter((item) => item.id !== String(itemId));
        writeCart(cartItems);
        updateCartView();
    }

    function updateCartNote(itemId, note) {
        const item = cartItems.find((cartItem) => cartItem.id === String(itemId));
        if (!item) return;
        item.note = note;
        writeCart(cartItems);
    }

    function renderFilteredDishes() {
        if (!dishesLoaded) return;
        const query = searchInputs[0]?.value.trim().toLocaleLowerCase('vi') ?? '';
        const filteredDishes = dishes.filter((dish) => String(dish.name ?? '').toLocaleLowerCase('vi').includes(query));
        if (filteredDishes.length === 0) {
            renderEmpty(dishesElement, dishes.length === 0 ? 'Chưa có món ăn trong danh mục này.' : 'Không tìm thấy món ăn phù hợp.');
            return;
        }
        // Truyền thêm hàm callback showDishDetail để kích hoạt nút ba chấm (⋮)
        renderDishes(dishesElement, filteredDishes, addToCart, showDishDetail);
    }

    function selectCategory(categoryId) {
        selectedCategoryId = categoryId;
        renderCategories(categoriesElement, categories, selectedCategoryId, selectCategory);
        loadDishes(categoryId);
    }

    async function loadDishes(categoryId) {
        const version = ++requestVersion;
        dishesLoaded = false;
        renderLoading(dishesElement, 'Đang tải thực đơn...');
        try {
            const response = await menuApi.getDishes(categoryId);
            if (version !== requestVersion) return;
            if (!Array.isArray(response)) throw new TypeError('Menu response must be an array.');
            dishes = response;
            dishesLoaded = true;
            renderFilteredDishes();
        } catch {
            if (version !== requestVersion) return;
            renderError(dishesElement, errorMessage, () => loadDishes(categoryId));
        }
    }

    async function loadMenu() {
        const version = ++requestVersion;
        dishesLoaded = false;
        renderLoading(categoriesElement, 'Đang tải danh mục...');
        renderLoading(dishesElement, 'Đang tải thực đơn...');
        try {
            const categoryResponse = await menuApi.getCategories();
            if (version !== requestVersion) return;
            if (!Array.isArray(categoryResponse)) throw new TypeError('Category response must be an array.');
            categories = categoryResponse;
            renderCategories(categoriesElement, categories, selectedCategoryId, selectCategory);

            const dishResponse = await menuApi.getDishes();
            if (version !== requestVersion) return;
            if (!Array.isArray(dishResponse)) throw new TypeError('Menu response must be an array.');
            selectedCategoryId = '';
            dishes = dishResponse;
            dishesLoaded = true;
            renderCategories(categoriesElement, categories, selectedCategoryId, selectCategory);
            renderFilteredDishes();
        } catch {
            if (version !== requestVersion) return;
            categoriesElement.replaceChildren();
            renderError(dishesElement, errorMessage, loadMenu);
        }
    }

    searchInputs.forEach((input) => {
        input.addEventListener('input', () => {
            searchInputs.forEach((otherInput) => {
                if (otherInput !== input) otherInput.value = input.value;
            });
            renderFilteredDishes();
        });
    });

    cartButton.addEventListener('click', () => setCartOpen(true));
    cartRoot.addEventListener('click', (event) => {
        if (event.target.closest('[data-cart-close]')) setCartOpen(false);
    });
    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') return;
        if (activeDishDetailId) {
            closeDishDetail();
            return;
        }
        if (cartIsOpen) setCartOpen(false);
    });

    async function reloadDishesSilently() {
        try {
            const [catRes, dishRes] = await Promise.all([
                menuApi.getCategories().catch(() => null),
                menuApi.getDishes(selectedCategoryId).catch(() => null)
            ]);
            if (Array.isArray(catRes)) {
                categories = catRes;
                renderCategories(categoriesElement, categories, selectedCategoryId, selectCategory);
            }
            if (Array.isArray(dishRes)) {
                dishes = dishRes;
                dishesLoaded = true;
                renderFilteredDishes();
            }
        } catch (e) {
            console.debug('Silent menu reload failed:', e);
        }
    }

    // Real-time synchronization
    realtimeClient.on('MenuItemUpdated', reloadDishesSilently);
    realtimeClient.on('DishStatusChanged', reloadDishesSilently);
    realtimeClient.on('sync', reloadDishesSilently);
    realtimeClient.start().catch(() => {});

    updateCartView();
    loadMenu();
}

initializeMenuPage();