# Change: Cải thiện giao diện Admin Quản lý Bài hát

## Why
Trang Admin/ManageMusic hiện tại có giao diện cơ bản, thiếu các tính năng UX quan trọng như tìm kiếm, phân trang, và preview file. Cần cải thiện để admin có thể quản lý bài hát hiệu quả hơn khi số lượng tăng lên.

## What Changes
- **UI/UX Improvements:**
  - Thêm thanh tìm kiếm và bộ lọc theo album
  - Thêm phân trang (pagination) cho danh sách bài hát
  - Cải thiện modal thêm bài hát với preview ảnh
  - Thêm loading indicator khi đang xử lý
  - Responsive design cho mobile
  
- **Visual Enhancements:**
  - Áp dụng Spotify dark theme nhất quán
  - Thêm hover effects và animations
  - Cải thiện typography và spacing
  - Thêm icons và badges cho trạng thái

- **Functional Improvements:**
  - Drag & drop upload file
  - Bulk actions (xóa nhiều bài hát)
  - Sắp xếp theo cột (sortable columns)
  - Toast notifications thay vì alerts

## Impact
- Affected views: `Views/Admin/ManageMusic.cshtml`, `Views/Admin/EditMusic.cshtml`
- Affected CSS: `wwwroot/css/site.css` (thêm admin styles)
- Affected JS: Thêm `wwwroot/js/admin.js`
- No breaking changes to existing functionality
