/* =====================================================
   ADMIN DASHBOARD JAVASCRIPT
   ===================================================== */

// Toast Notifications
const Toast = {
    container: null,
    
    init() {
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.className = 'toast-container';
            document.body.appendChild(this.container);
        }
    },
    
    show(message, type = 'success', duration = 3000) {
        this.init();
        
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        
        const icons = {
            success: '✓',
            error: '✕',
            warning: '!'
        };
        
        toast.innerHTML = `
            <div class="toast-icon">${icons[type] || icons.success}</div>
            <div class="toast-message">${message}</div>
            <button class="toast-close" onclick="this.parentElement.remove()">×</button>
        `;
        
        this.container.appendChild(toast);
        
        setTimeout(() => {
            toast.classList.add('hiding');
            setTimeout(() => toast.remove(), 300);
        }, duration);
    },
    
    success(message) { this.show(message, 'success'); },
    error(message) { this.show(message, 'error'); },
    warning(message) { this.show(message, 'warning'); }
};

// Admin Table with Search, Filter, Sort, Pagination
class AdminTable {
    constructor(options) {
        this.tableBody = document.querySelector(options.tableBody);
        this.searchInput = document.querySelector(options.searchInput);
        this.filterSelect = document.querySelector(options.filterSelect);
        this.paginationContainer = document.querySelector(options.pagination);
        this.data = options.data || [];
        this.filteredData = [...this.data];
        this.pageSize = options.pageSize || 10;
        this.currentPage = 1;
        this.sortColumn = null;
        this.sortDirection = 'asc';
        this.renderRow = options.renderRow;
        
        this.init();
    }
    
    init() {
        if (this.searchInput) {
            this.searchInput.addEventListener('input', (e) => {
                this.search(e.target.value);
            });
        }
        
        if (this.filterSelect) {
            this.filterSelect.addEventListener('change', (e) => {
                this.filter(e.target.value);
            });
        }
        
        document.querySelectorAll('[data-sort]').forEach(th => {
            th.addEventListener('click', () => {
                this.sort(th.dataset.sort);
            });
        });
        
        this.render();
    }
    
    search(query) {
        query = query.toLowerCase().trim();
        this.filteredData = this.data.filter(item => {
            return item.name.toLowerCase().includes(query) ||
                   (item.album && item.album.toLowerCase().includes(query));
        });
        this.currentPage = 1;
        this.render();
    }
    
    filter(albumId) {
        if (!albumId) {
            this.filteredData = [...this.data];
        } else {
            this.filteredData = this.data.filter(item => item.albumId === albumId);
        }
        this.currentPage = 1;
        this.render();
    }
    
    sort(column) {
        if (this.sortColumn === column) {
            this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
        } else {
            this.sortColumn = column;
            this.sortDirection = 'asc';
        }
        
        document.querySelectorAll('[data-sort]').forEach(th => {
            th.classList.remove('sort-asc', 'sort-desc');
        });
        
        const th = document.querySelector(`[data-sort="${column}"]`);
        if (th) {
            th.classList.add(this.sortDirection === 'asc' ? 'sort-asc' : 'sort-desc');
        }
        
        this.filteredData.sort((a, b) => {
            let valA = a[column];
            let valB = b[column];
            
            if (column === 'date') {
                valA = new Date(valA);
                valB = new Date(valB);
            }
            
            if (typeof valA === 'string') {
                valA = valA.toLowerCase();
                valB = valB.toLowerCase();
            }
            
            if (valA < valB) return this.sortDirection === 'asc' ? -1 : 1;
            if (valA > valB) return this.sortDirection === 'asc' ? 1 : -1;
            return 0;
        });
        
        this.render();
    }
    
    goToPage(page) {
        this.currentPage = page;
        this.render();
    }
    
    render() {
        const start = (this.currentPage - 1) * this.pageSize;
        const end = start + this.pageSize;
        const pageData = this.filteredData.slice(start, end);
        
        if (this.tableBody && this.renderRow) {
            if (pageData.length === 0) {
                this.tableBody.innerHTML = `
                    <tr>
                        <td colspan="5">
                            <div class="empty-state">
                                <i class="fas fa-music"></i>
                                <h3>Không có bài hát</h3>
                                <p>Thêm bài hát mới để bắt đầu</p>
                            </div>
                        </td>
                    </tr>
                `;
            } else {
                this.tableBody.innerHTML = pageData.map(item => this.renderRow(item)).join('');
            }
        }
        
        this.renderPagination();
    }
    
    renderPagination() {
        if (!this.paginationContainer) return;
        
        const totalPages = Math.ceil(this.filteredData.length / this.pageSize);
        const start = (this.currentPage - 1) * this.pageSize + 1;
        const end = Math.min(this.currentPage * this.pageSize, this.filteredData.length);
        
        let paginationHTML = `
            <div class="pagination-info">
                Hiển thị ${start}-${end} trong ${this.filteredData.length} bài hát
            </div>
            <div class="pagination">
                <button onclick="adminTable.goToPage(${this.currentPage - 1})" ${this.currentPage === 1 ? 'disabled' : ''}>
                    <i class="fas fa-chevron-left"></i>
                </button>
        `;
        
        for (let i = 1; i <= totalPages; i++) {
            if (i === 1 || i === totalPages || (i >= this.currentPage - 1 && i <= this.currentPage + 1)) {
                paginationHTML += `
                    <button onclick="adminTable.goToPage(${i})" class="${i === this.currentPage ? 'active' : ''}">
                        ${i}
                    </button>
                `;
            } else if (i === this.currentPage - 2 || i === this.currentPage + 2) {
                paginationHTML += `<button disabled>...</button>`;
            }
        }
        
        paginationHTML += `
                <button onclick="adminTable.goToPage(${this.currentPage + 1})" ${this.currentPage === totalPages ? 'disabled' : ''}>
                    <i class="fas fa-chevron-right"></i>
                </button>
            </div>
        `;
        
        this.paginationContainer.innerHTML = paginationHTML;
    }
}

// Modal Functions
function openModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.add('show');
        document.body.style.overflow = 'hidden';
    }
}

function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.remove('show');
        document.body.style.overflow = '';
    }
}

// Close modal on backdrop click
document.addEventListener('click', (e) => {
    if (e.target.classList.contains('admin-modal')) {
        e.target.classList.remove('show');
        document.body.style.overflow = '';
    }
});

// Image Preview
function previewImage(input, previewId) {
    const preview = document.getElementById(previewId);
    if (!preview) return;
    
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = (e) => {
            preview.innerHTML = `
                <img src="${e.target.result}" alt="Preview">
                <div class="file-name">${input.files[0].name}</div>
            `;
        };
        reader.readAsDataURL(input.files[0]);
    } else {
        preview.innerHTML = '';
    }
}

// File Upload Drag & Drop
function initDragDrop(uploadId) {
    const uploadZone = document.getElementById(uploadId);
    if (!uploadZone) return;
    
    ['dragenter', 'dragover'].forEach(event => {
        uploadZone.addEventListener(event, (e) => {
            e.preventDefault();
            uploadZone.classList.add('dragover');
        });
    });
    
    ['dragleave', 'drop'].forEach(event => {
        uploadZone.addEventListener(event, (e) => {
            e.preventDefault();
            uploadZone.classList.remove('dragover');
        });
    });
}

// Delete Music Function
function deleteMusic(id, name) {
    if (confirm(`Bạn có chắc muốn xóa bài hát "${name}"?`)) {
        fetch('/Admin/DeleteMusic/' + id, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        })
        .then(response => {
            if (response.ok) {
                Toast.success('Xóa bài hát thành công!');
                setTimeout(() => location.reload(), 1000);
            } else {
                Toast.error('Xóa bài hát thất bại');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            Toast.error('Có lỗi xảy ra');
        });
    }
}

// Edit Music Function
function editMusic(id) {
    window.location.href = '/Admin/EditMusic/' + id;
}

// Form Submit with Loading
function submitFormWithLoading(formId) {
    const form = document.getElementById(formId);
    if (!form) return;
    
    const overlay = document.createElement('div');
    overlay.className = 'loading-overlay';
    overlay.innerHTML = '<div class="spinner"></div>';
    document.body.appendChild(overlay);
    
    form.submit();
}

// Initialize on DOM Ready
document.addEventListener('DOMContentLoaded', () => {
    // Init drag drop zones
    document.querySelectorAll('.file-upload').forEach((el, index) => {
        initDragDrop(el.id || `upload-${index}`);
    });
    
    // Show toast from TempData
    const successMsg = document.querySelector('[data-toast-success]');
    const errorMsg = document.querySelector('[data-toast-error]');
    
    if (successMsg && successMsg.dataset.toastSuccess) {
        Toast.success(successMsg.dataset.toastSuccess);
    }
    if (errorMsg && errorMsg.dataset.toastError) {
        Toast.error(errorMsg.dataset.toastError);
    }
});
