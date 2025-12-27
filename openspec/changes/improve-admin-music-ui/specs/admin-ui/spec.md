## ADDED Requirements

### Requirement: Admin Music Search
Trang quản lý bài hát PHẢI có thanh tìm kiếm cho phép admin tìm bài hát theo tên.

#### Scenario: Search by song name
- **WHEN** admin nhập từ khóa vào thanh tìm kiếm
- **THEN** danh sách bài hát được lọc real-time theo tên chứa từ khóa

#### Scenario: Clear search
- **WHEN** admin xóa nội dung thanh tìm kiếm
- **THEN** hiển thị lại toàn bộ danh sách bài hát

### Requirement: Admin Music Filter by Album
Trang quản lý bài hát PHẢI có bộ lọc cho phép admin lọc bài hát theo album.

#### Scenario: Filter by album
- **WHEN** admin chọn một album từ dropdown
- **THEN** chỉ hiển thị các bài hát thuộc album đó

#### Scenario: Show all albums
- **WHEN** admin chọn "Tất cả album"
- **THEN** hiển thị toàn bộ bài hát

### Requirement: Admin Music Pagination
Trang quản lý bài hát PHẢI có phân trang khi có nhiều bài hát.

#### Scenario: Display pagination
- **WHEN** có hơn 10 bài hát
- **THEN** hiển thị pagination controls ở cuối bảng

#### Scenario: Navigate pages
- **WHEN** admin click vào số trang hoặc prev/next
- **THEN** hiển thị bài hát của trang tương ứng

### Requirement: Admin Music Upload Preview
Modal thêm bài hát PHẢI hiển thị preview ảnh khi admin chọn file ảnh.

#### Scenario: Preview selected image
- **WHEN** admin chọn file ảnh trong modal thêm bài hát
- **THEN** hiển thị preview ảnh ngay trong modal

### Requirement: Admin Toast Notifications
Hệ thống PHẢI hiển thị toast notifications thay vì alert boxes.

#### Scenario: Success notification
- **WHEN** thao tác thành công (thêm/sửa/xóa bài hát)
- **THEN** hiển thị toast màu xanh với message thành công

#### Scenario: Error notification  
- **WHEN** thao tác thất bại
- **THEN** hiển thị toast màu đỏ với message lỗi

## MODIFIED Requirements

### Requirement: Admin Music Table Display
Bảng danh sách bài hát PHẢI có giao diện modern với Spotify dark theme, hỗ trợ hover effects và responsive design.

#### Scenario: Table hover effect
- **WHEN** admin hover qua một row trong bảng
- **THEN** row được highlight với màu nền nhạt hơn

#### Scenario: Responsive table
- **WHEN** truy cập trên thiết bị mobile
- **THEN** bảng hiển thị dạng card hoặc scroll horizontal
