using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MyWebApp.Models;

namespace MyWebApp.Mvc.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiService(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        private void SetAuthToken()
        {
            var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        // Generic GET method with ApiResponse unwrapping
        public async Task<T?> GetAsync<T>(string endpoint)
        {
            SetAuthToken();
            var response = await _httpClient.GetAsync(endpoint);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ApiService] GET {endpoint} failed with status: {(int)response.StatusCode}");
                return default;
            }

            var content = await response.Content.ReadAsStringAsync();
            
            // Check if response is valid JSON
            if (string.IsNullOrWhiteSpace(content) || (!content.TrimStart().StartsWith("{") && !content.TrimStart().StartsWith("[")))
            {
                Console.WriteLine($"[ApiService] GET {endpoint} returned non-JSON response: {content.Substring(0, Math.Min(100, content.Length))}");
                return default;
            }
            
            // Try to deserialize as ApiResponse<T> first (for wrapped responses)
            try
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (apiResponse != null && apiResponse.Success)
                {
                    return apiResponse.Data;
                }
            }
            catch
            {
                // If it fails, try direct deserialization (for non-wrapped responses)
            }
            
            // Fallback to direct deserialization
            try
            {
                return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApiService] Failed to deserialize response from {endpoint}: {ex.Message}");
                return default;
            }
        }

        // Generic POST method with ApiResponse unwrapping
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            SetAuthToken();
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(endpoint, content);
            
            if (!response.IsSuccessStatusCode)
                return default;

            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Try to deserialize as ApiResponse<TResponse> first
            try
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(responseContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (apiResponse != null && apiResponse.Success)
                {
                    return apiResponse.Data;
                }
            }
            catch
            {
                // If it fails, try direct deserialization
            }
            
            return JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }

        // Generic PUT method with ApiResponse unwrapping
        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            SetAuthToken();
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync(endpoint, content);
            
            if (!response.IsSuccessStatusCode)
                return default;

            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Try to deserialize as ApiResponse<TResponse> first
            try
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(responseContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (apiResponse != null && apiResponse.Success)
                {
                    return apiResponse.Data;
                }
            }
            catch
            {
                // If it fails, try direct deserialization
            }
            
            return JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }

        // Generic DELETE method
        public async Task<bool> DeleteAsync(string endpoint)
        {
            SetAuthToken();
            var response = await _httpClient.DeleteAsync(endpoint);
            return response.IsSuccessStatusCode;
        }

        // Multipart form data for file uploads with ApiResponse unwrapping
        public async Task<TResponse?> PostMultipartAsync<TResponse>(string endpoint, MultipartFormDataContent content)
        {
            SetAuthToken();
            
            // Debug log
            var hasAuth = _httpClient.DefaultRequestHeaders.Authorization != null;
            Console.WriteLine($"[ApiService] PostMultipartAsync to {endpoint}");
            Console.WriteLine($"[ApiService] Has Authorization header: {hasAuth}");
            if (hasAuth)
                Console.WriteLine($"[ApiService] Auth scheme: {_httpClient.DefaultRequestHeaders.Authorization?.Scheme}");
            
            var response = await _httpClient.PostAsync(endpoint, content);
            Console.WriteLine($"[ApiService] Response status: {(int)response.StatusCode} {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ApiService] Error response: {errorContent.Substring(0, Math.Min(500, errorContent.Length))}");
                return default;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Try to deserialize as ApiResponse<TResponse> first
            try
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(responseContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (apiResponse != null && apiResponse.Success)
                {
                    return apiResponse.Data;
                }
            }
            catch
            {
                // If it fails, try direct deserialization
            }
            
            return JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }

        // Multipart form data for PUT requests
        public async Task<TResponse?> PutMultipartAsync<TResponse>(string endpoint, MultipartFormDataContent content)
        {
            SetAuthToken();
            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
            {
                Content = content
            };
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
                return default;

            var responseContent = await response.Content.ReadAsStringAsync();
            
            try
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(responseContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (apiResponse != null && apiResponse.Success)
                {
                    return apiResponse.Data;
                }
            }
            catch
            {
            }
            
            return JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
    }

    // API Response wrapper
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}
