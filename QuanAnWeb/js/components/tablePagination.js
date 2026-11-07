/**
 * Component phân trang danh sách bàn dùng chung cho Website GustoPOS (Checkout & Booking)
 * 
 * Đáp ứng các yêu cầu:
 * - Mặc định 10 bàn/trang
 * - Nút [Trang trước] và [Trang sau]
 * - Hiển thị số trang hiện tại và tổng số trang (VD: Hiển thị: 1–10 / 45 bàn (Trang 1/5))
 * - Cho phép chuyển trực tiếp đến từng số trang [1] [2] [3]...
 * - Lựa chọn số lượng bàn mỗi trang: 10, 20, 50 bàn/trang
 * - Tự động vô hiệu hóa Trang trước ở trang đầu, Trang sau ở trang cuối
 * - Responsive trên màn hình máy tính, tablet và điện thoại
 */

export function renderTablePagination(container, {
    currentPage = 1,
    totalPages = 1,
    totalItems = 0,
    pageSize = 10,
    pageSizeOptions = [10, 20, 50],
    onPageChange = null,
    onPageSizeChange = null
} = {}) {
    if (!container) return;

    if (totalItems <= 0) {
        container.className = 'w-full min-w-0 hidden';
        container.replaceChildren();
        return;
    }

    container.className = 'w-full min-w-0 pt-3.5 border-t border-[#e4beb4]/50 mt-3';
    container.replaceChildren();

    const startIndex = (currentPage - 1) * pageSize + 1;
    const endIndex = Math.min(currentPage * pageSize, totalItems);

    const wrapper = document.createElement('div');
    wrapper.className = 'flex flex-col md:flex-row items-center justify-between gap-3 text-xs w-full min-w-0';

    // 1. Bên trái: Thông tin hiển thị "Hiển thị: X–Y / Z bàn (Trang A/B)"
    const infoBox = document.createElement('div');
    infoBox.className = 'flex items-center gap-1.5 text-[#5b4139] font-medium shrink-0';
    infoBox.innerHTML = `
        <span>Hiển thị: <strong class="text-[#1a1c1c] font-bold">${startIndex}–${endIndex}</strong> / <strong class="text-[#1a1c1c] font-bold">${totalItems}</strong> bàn</span>
        <span class="text-[#8c7067] ml-1">(Trang ${currentPage}/${totalPages})</span>
    `;

    // 2. Ở giữa: Bộ điều hướng [Trang trước] [1] [2]... [Trang sau]
    const nav = document.createElement('nav');
    nav.className = 'flex items-center gap-1.5 flex-wrap justify-center';
    nav.setAttribute('aria-label', 'Phân trang danh sách bàn');

    // Nút [Trang trước]
    const prevBtn = document.createElement('button');
    prevBtn.type = 'button';
    const isPrevDisabled = currentPage <= 1;
    prevBtn.disabled = isPrevDisabled;
    prevBtn.className = `inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg font-semibold transition-colors ${
        isPrevDisabled
            ? 'opacity-40 cursor-not-allowed bg-[#f3f3f3] text-[#5b4139]'
            : 'bg-[#f3f3f3] text-[#1a1c1c] hover:bg-[#e8e8e8] hover:text-[#ac2d00] cursor-pointer'
    }`;
    prevBtn.innerHTML = `
        <span class="material-symbols-outlined text-[16px]">chevron_left</span>
        <span>Trang trước</span>
    `;
    prevBtn.addEventListener('click', () => {
        if (!isPrevDisabled && typeof onPageChange === 'function') {
            onPageChange(currentPage - 1);
        }
    });
    nav.appendChild(prevBtn);

    // Các nút số trang
    const pages = getPaginationPages(currentPage, totalPages);
    pages.forEach((p) => {
        if (p === '...') {
            const ellipsis = document.createElement('span');
            ellipsis.className = 'w-7 h-7 flex items-center justify-center text-[#5b4139] font-bold select-none';
            ellipsis.textContent = '…';
            nav.appendChild(ellipsis);
        } else {
            const pageNum = Number(p);
            const isCurrent = pageNum === currentPage;
            const pageBtn = document.createElement('button');
            pageBtn.type = 'button';
            pageBtn.className = `min-w-[30px] h-7 px-2 rounded-lg font-semibold transition-all flex items-center justify-center ${
                isCurrent
                    ? 'bg-[#ac2d00] text-white shadow-xs font-bold cursor-default ring-2 ring-[#ac2d00]/20'
                    : 'bg-[#f3f3f3] text-[#1a1c1c] hover:bg-[#e8e8e8] hover:text-[#ac2d00] cursor-pointer'
            }`;
            pageBtn.textContent = String(pageNum);
            pageBtn.setAttribute('aria-current', isCurrent ? 'page' : 'false');
            pageBtn.addEventListener('click', () => {
                if (!isCurrent && typeof onPageChange === 'function') {
                    onPageChange(pageNum);
                }
            });
            nav.appendChild(pageBtn);
        }
    });

    // Nút [Trang sau]
    const nextBtn = document.createElement('button');
    nextBtn.type = 'button';
    const isNextDisabled = currentPage >= totalPages;
    nextBtn.disabled = isNextDisabled;
    nextBtn.className = `inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg font-semibold transition-colors ${
        isNextDisabled
            ? 'opacity-40 cursor-not-allowed bg-[#f3f3f3] text-[#5b4139]'
            : 'bg-[#f3f3f3] text-[#1a1c1c] hover:bg-[#e8e8e8] hover:text-[#ac2d00] cursor-pointer'
    }`;
    nextBtn.innerHTML = `
        <span>Trang sau</span>
        <span class="material-symbols-outlined text-[16px]">chevron_right</span>
    `;
    nextBtn.addEventListener('click', () => {
        if (!isNextDisabled && typeof onPageChange === 'function') {
            onPageChange(currentPage + 1);
        }
    });
    nav.appendChild(nextBtn);

    // 3. Bên phải: Chọn số lượng bàn/trang "Số bàn/trang: [10 ▼]"
    const sizeBox = document.createElement('div');
    sizeBox.className = 'flex items-center gap-2 text-[#5b4139] font-medium shrink-0';

    const sizeLabel = document.createElement('label');
    const sizeSelectId = `page-size-${Math.random().toString(36).substring(2, 7)}`;
    sizeLabel.htmlFor = sizeSelectId;
    sizeLabel.textContent = 'Số bàn/trang:';

    const sizeSelect = document.createElement('select');
    sizeSelect.id = sizeSelectId;
    sizeSelect.className = 'rounded-lg border border-[#d8c8c2] bg-white py-1 pl-2.5 pr-7 text-xs font-semibold text-[#1a1c1c] shadow-xs focus:border-[#ac2d00] focus:outline-none focus:ring-1 focus:ring-[#ac2d00] cursor-pointer';

    pageSizeOptions.forEach((opt) => {
        const option = document.createElement('option');
        option.value = String(opt);
        option.textContent = String(opt);
        if (opt === pageSize) {
            option.selected = true;
        }
        sizeSelect.appendChild(option);
    });

    sizeSelect.addEventListener('change', () => {
        const newSize = Number(sizeSelect.value);
        if (newSize > 0 && typeof onPageSizeChange === 'function') {
            onPageSizeChange(newSize);
        }
    });

    sizeBox.appendChild(sizeLabel);
    sizeBox.appendChild(sizeSelect);

    wrapper.appendChild(infoBox);
    wrapper.appendChild(nav);
    wrapper.appendChild(sizeBox);
    container.appendChild(wrapper);
}

export function getPaginationPages(currentPage, totalPages) {
    if (totalPages <= 7) {
        return Array.from({ length: totalPages }, (_, i) => i + 1);
    }
    if (currentPage <= 4) {
        return [1, 2, 3, 4, 5, '...', totalPages];
    }
    if (currentPage >= totalPages - 3) {
        return [1, '...', totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
    }
    return [1, '...', currentPage - 1, currentPage, currentPage + 1, '...', totalPages];
}
