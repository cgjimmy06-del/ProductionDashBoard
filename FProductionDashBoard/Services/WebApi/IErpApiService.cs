using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services.WebApi;

public interface IErpApiService
{
    Task<EmpInfoDto?> GetEmpInfoByCardAsync(string factoryArea, string empCardNumber);
}
