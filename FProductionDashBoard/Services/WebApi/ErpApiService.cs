using System.Net.Http;
using System.Net.Http.Json;
using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services.WebApi;

public class ErpApiService : IErpApiService
{
    private readonly HttpClient _http;

    public ErpApiService(HttpClient http) => _http = http;

    public async Task<EmpInfoDto?> GetEmpInfoByCardAsync(string factoryArea, string empCardNumber)
    {
        try
        {
            return await _http.GetFromJsonAsync<EmpInfoDto>(
                $"api/Po/GetEmpInfoByCard/{Uri.EscapeDataString(factoryArea)}/{Uri.EscapeDataString(empCardNumber)}");
        }
        catch
        {
            return null;
        }
    }
}
