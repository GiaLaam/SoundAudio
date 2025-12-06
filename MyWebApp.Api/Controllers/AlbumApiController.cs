using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;
using MyWebApp.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlbumApiController : ControllerBase
    {
        private readonly AlbumService _albumService;
        private readonly MusicService _musicService;

        public AlbumApiController(AlbumService albumService, MusicService musicService)
        {
            _albumService = albumService;
            _musicService = musicService;
        }

        /// <summary>
        /// Lấy tất cả albums
        /// GET: api/AlbumApi
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<Album>>>> GetAllAlbums()
        {
            try
            {
                var albums = await _albumService.GetAllAsync();
                return Ok(new ApiResponse<List<Album>>
                {
                    Success = true,
                    Message = "Lấy danh sách album thành công",
                    Data = albums
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<Album>>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Lấy album theo ID
        /// GET: api/AlbumApi/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<Album>>> GetAlbumById(string id)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Không tìm thấy album",
                        Data = null
                    });
                }

                return Ok(new ApiResponse<Album>
                {
                    Success = true,
                    Message = "Lấy thông tin album thành công",
                    Data = album
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<Album>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Lấy tất cả bài hát trong album
        /// GET: api/AlbumApi/{id}/songs
        /// </summary>
        [HttpGet("{id}/songs")]
        public async Task<ActionResult<ApiResponse<List<MusicFile>>>> GetSongsByAlbum(string id)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new ApiResponse<List<MusicFile>>
                    {
                        Success = false,
                        Message = "Không tìm thấy album",
                        Data = null
                    });
                }

                var songs = await _musicService.GetSongsByAlbum(id);
                return Ok(new ApiResponse<List<MusicFile>>
                {
                    Success = true,
                    Message = $"Lấy danh sách bài hát trong album '{album.Name}' thành công",
                    Data = songs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<MusicFile>>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết album kèm danh sách bài hát
        /// GET: api/AlbumApi/{id}/details
        /// </summary>
        [HttpGet("{id}/details")]
        public async Task<ActionResult<ApiResponse<AlbumDetailResponse>>> GetAlbumDetails(string id)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new ApiResponse<AlbumDetailResponse>
                    {
                        Success = false,
                        Message = "Không tìm thấy album",
                        Data = null
                    });
                }

                var songs = await _musicService.GetSongsByAlbum(id);
                
                var response = new AlbumDetailResponse
                {
                    Album = album,
                    Songs = songs,
                    TotalSongs = songs.Count
                };

                return Ok(new ApiResponse<AlbumDetailResponse>
                {
                    Success = true,
                    Message = "Lấy chi tiết album thành công",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<AlbumDetailResponse>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Tạo album mới
        /// POST: api/AlbumApi
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<Album>>> CreateAlbum([FromBody] CreateAlbumRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Tên album không được để trống",
                        Data = null
                    });
                }

                var album = new Album
                {
                    Name = request.Name,
                    ImageUrl = request.ImageUrl,
                    CreatedAt = DateTime.UtcNow
                };

                await _albumService.CreateAsync(album);

                return CreatedAtAction(
                    nameof(GetAlbumById),
                    new { id = album.Id },
                    new ApiResponse<Album>
                    {
                        Success = true,
                        Message = "Tạo album thành công",
                        Data = album
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<Album>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Cập nhật album
        /// PUT: api/AlbumApi/{id}
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<Album>>> UpdateAlbum(string id, [FromBody] UpdateAlbumRequest request)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Không tìm thấy album",
                        Data = null
                    });
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                    album.Name = request.Name;

                if (!string.IsNullOrWhiteSpace(request.ImageUrl))
                    album.ImageUrl = request.ImageUrl;

                await _albumService.UpdateAsync(id, album);

                return Ok(new ApiResponse<Album>
                {
                    Success = true,
                    Message = "Cập nhật album thành công",
                    Data = album
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<Album>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Xóa album
        /// DELETE: api/AlbumApi/{id}
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteAlbum(string id)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Không tìm thấy album",
                        Data = false
                    });
                }

                // Kiểm tra xem album có bài hát không
                var songs = await _musicService.GetSongsByAlbum(id);
                if (songs.Count > 0)
                {
                    return BadRequest(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = $"Không thể xóa album vì còn {songs.Count} bài hát. Vui lòng xóa hoặc chuyển các bài hát trước.",
                        Data = false
                    });
                }

                await _albumService.DeleteAsync(id);

                return Ok(new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Xóa album thành công",
                    Data = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = false
                });
            }
        }

        /// <summary>
        /// Lấy ảnh của album theo ID
        /// GET: api/AlbumApi/{id}/image
        /// </summary>
        [HttpGet("{id}/image")]
        public async Task<IActionResult> GetAlbumImage(string id)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy album" });
                }

                if (string.IsNullOrWhiteSpace(album.ImageUrl))
                {
                    return NotFound(new { success = false, message = "Album chưa có ảnh" });
                }

                // Trích xuất tên file từ ImageUrl (format: /api/images/{fileName})
                var fileName = album.ImageUrl.Replace("/api/images/", "");
                
                // Redirect đến ImageApiController
                return Redirect($"/api/images/{fileName}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        /// <summary>
        /// Upload ảnh cho album (multipart/form-data)
        /// POST: api/AlbumApi/{id}/upload-image
        /// </summary>
        [HttpPost("{id}/upload-image")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<Album>>> UploadAlbumImage(string id, [FromForm] UploadAlbumImageRequest request)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Không tìm thấy album",
                        Data = null
                    });
                }

                if (request.Image == null || request.Image.Length == 0)
                {
                    return BadRequest(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Vui lòng chọn file ảnh",
                        Data = null
                    });
                }

                var ext = Path.GetExtension(request.Image.FileName).ToLower();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                {
                    return BadRequest(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Chỉ chấp nhận file ảnh định dạng .jpg, .jpeg, .png",
                        Data = null
                    });
                }

                // Upload ảnh lên GridFS
                var imageName = "album_" + Path.GetFileNameWithoutExtension(request.Image.FileName).Replace(" ", "").ToLower() + ext;
                byte[] imageBytes;
                using (var ms = new MemoryStream())
                {
                    await request.Image.CopyToAsync(ms);
                    imageBytes = ms.ToArray();
                }
                
                var imageId = await _musicService.UploadImageAsync(imageBytes, imageName);
                album.ImageUrl = $"/api/images/{imageName}";

                await _albumService.UpdateAsync(id, album);

                return Ok(new ApiResponse<Album>
                {
                    Success = true,
                    Message = "Upload ảnh album thành công",
                    Data = album
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<Album>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Cập nhật album với ảnh (multipart/form-data)
        /// PUT: api/AlbumApi/{id}/update-with-image
        /// </summary>
        [HttpPut("{id}/update-with-image")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<Album>>> UpdateAlbumWithImage(
            string id,
            [FromForm] UpdateAlbumWithImageRequest request)
        {
            try
            {
                var album = await _albumService.GetByIdAsync(id);
                if (album == null)
                {
                    return NotFound(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Không tìm thấy album",
                        Data = null
                    });
                }

                // Cập nhật tên nếu có
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    album.Name = request.Name;
                }

                // Upload ảnh nếu có
                if (request.Image != null && request.Image.Length > 0)
                {
                    var ext = Path.GetExtension(request.Image.FileName).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    {
                        var imageName = "album_" + Path.GetFileNameWithoutExtension(request.Image.FileName).Replace(" ", "").ToLower() + ext;
                        byte[] imageBytes;
                        using (var ms = new MemoryStream())
                        {
                            await request.Image.CopyToAsync(ms);
                            imageBytes = ms.ToArray();
                        }
                        
                        var imageId = await _musicService.UploadImageAsync(imageBytes, imageName);
                        album.ImageUrl = $"/api/images/{imageName}";
                    }
                    else
                    {
                        return BadRequest(new ApiResponse<Album>
                        {
                            Success = false,
                            Message = "Chỉ chấp nhận file ảnh định dạng .jpg, .jpeg, .png",
                            Data = null
                        });
                    }
                }

                await _albumService.UpdateAsync(id, album);

                return Ok(new ApiResponse<Album>
                {
                    Success = true,
                    Message = "Cập nhật album thành công",
                    Data = album
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<Album>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Tạo album mới với ảnh (multipart/form-data)
        /// POST: api/AlbumApi/create-with-image
        /// </summary>
        [HttpPost("create-with-image")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<Album>>> CreateAlbumWithImage(
            [FromForm] CreateAlbumWithImageRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new ApiResponse<Album>
                    {
                        Success = false,
                        Message = "Tên album không được để trống",
                        Data = null
                    });
                }

                var album = new Album
                {
                    Name = request.Name,
                    CreatedAt = DateTime.UtcNow
                };

                // Upload ảnh nếu có
                if (request.Image != null && request.Image.Length > 0)
                {
                    var ext = Path.GetExtension(request.Image.FileName).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    {
                        var imageName = "album_" + Path.GetFileNameWithoutExtension(request.Image.FileName).Replace(" ", "").ToLower() + ext;
                        byte[] imageBytes;
                        using (var ms = new MemoryStream())
                        {
                            await request.Image.CopyToAsync(ms);
                            imageBytes = ms.ToArray();
                        }
                        
                        var imageId = await _musicService.UploadImageAsync(imageBytes, imageName);
                        album.ImageUrl = $"/api/images/{imageName}";
                    }
                    else
                    {
                        return BadRequest(new ApiResponse<Album>
                        {
                            Success = false,
                            Message = "Chỉ chấp nhận file ảnh định dạng .jpg, .jpeg, .png",
                            Data = null
                        });
                    }
                }

                await _albumService.CreateAsync(album);

                return CreatedAtAction(
                    nameof(GetAlbumById),
                    new { id = album.Id },
                    new ApiResponse<Album>
                    {
                        Success = true,
                        Message = "Tạo album thành công",
                        Data = album
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<Album>
                {
                    Success = false,
                    Message = $"Lỗi: {ex.Message}",
                    Data = null
                });
            }
        }
    }

    // DTO Classes
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class AlbumDetailResponse
    {
        public Album? Album { get; set; }
        public List<MusicFile> Songs { get; set; } = new List<MusicFile>();
        public int TotalSongs { get; set; }
    }

    public class CreateAlbumRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }

    public class UpdateAlbumRequest
    {
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
    }
}
