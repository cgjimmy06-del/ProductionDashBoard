using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services.WebApi;

public interface IErpApiService
{
    Task<EmpInfoDto?> GetEmpInfoByCardAsync(string empCardNumber);

    /// <summary>以 8 碼件號查 ERP 品項資料（品名/品牌等）；查無或失敗回 null。</summary>
    Task<PartInfoDto?> GetPartInfoByNoAsync(string partNo);
}
