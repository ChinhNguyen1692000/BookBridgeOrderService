using System.Security.Cryptography;
using System.Text;
using System.Web; // Cần thêm System.Web.dll nếu dùng .NET Framework, hoặc HttpUtility.UrlEncode/Decode trong .NET Core/5+

public class VnPayLibrary
{
    private SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
    private SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

    // Thêm dữ liệu cho Request (tạo URL)
    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData.Add(key, value);
        }
    }

    // Thêm dữ liệu cho Response (xác thực Callback)
    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData.Add(key, value);
        }
    }

    // Lấy dữ liệu từ Response (dùng trong Callback)
    public string GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out var value) ? value : string.Empty;
    }

    // 1. Tạo URL Thanh toán (và tính Hash)
    public string CreateRequestUrl(string baseUrl, string hashSecret)
    {
        var data = new StringBuilder();
        foreach (var (key, value) in _requestData)
        {
            // Nối các tham số lại theo thứ tự ABC
            data.Append(HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(value) + "&");
        }
        
        var rawData = string.Join("&", _requestData.Select(kv => kv.Key + "=" + kv.Value));
        
        // Tính SecureHash (SHA256)
        string secureHash = HmacSHA512(hashSecret, rawData);

        return baseUrl + "?" + data.ToString() + "vnp_SecureHash=" + secureHash;
    }

    // 2. Validate SecureHash (Dùng cho ReturnUrl và IPN URL)
    public bool ValidateSignature(string receivedHash, string hashSecret)
    {
        // Loại bỏ vnp_SecureHash khỏi danh sách tham số để tính lại
        if (_responseData.ContainsKey("vnp_SecureHash"))
        {
            _responseData.Remove("vnp_SecureHash");
        }

        var rawData = string.Join("&", _responseData.Select(kv => kv.Key + "=" + kv.Value));
        string calculatedHash = HmacSHA512(hashSecret, rawData);
        
        return calculatedHash.Equals(receivedHash, StringComparison.OrdinalIgnoreCase);
    }
    
    // Hàm Hashing (VNPAY dùng HMACSHA512)
    private string HmacSHA512(string key, string input)
    {
        var hash = new StringBuilder();
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        
        using (var hmac = new HMACSHA512(keyBytes))
        {
            byte[] hashBytes = hmac.ComputeHash(inputBytes);
            foreach (var b in hashBytes)
            {
                hash.Append(b.ToString("x2"));
            }
        }
        return hash.ToString();
    }
}

// Custom Comparer để đảm bảo sắp xếp các tham số theo thứ tự ABC
public class VnPayCompare : IComparer<string>
{
    public int Compare(string x, string y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return string.Compare(x, y, StringComparison.Ordinal);
    }
}