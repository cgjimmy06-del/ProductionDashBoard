using FProductionDashBoard.Dtos;
using FProductionDashBoard.UiModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FProductionDashBoard.Services
{
    public class CardReaderHandler
    {
        private readonly DashboardCoreServices _core;
        private readonly WebApi.IErpApiService _erpApiService;
        private readonly IDialogService _dialog;
        private readonly ListsFromSql _commonLists;

        public CardReaderHandler(DashboardCoreServices core,
            WebApi.IErpApiService erpApiService,
            IDialogService dialogService,
            ListsFromSql commonLists)
        {
            _core = core;
            _erpApiService = erpApiService;
            _dialog = dialogService;
            _commonLists = commonLists;
        }

        public void Attach() => _core.CardReader.CardRead += OnCardRead;
        public void Detach() => _core.CardReader.CardRead -= OnCardRead;

        private async void OnCardRead(object? sender, CardReadEventArgs e)
        {
            try
            {
                var snapshot = _commonLists.UsersList.ToList();
                var user = snapshot.FirstOrDefault(u => u.CardId == e.CardId);

                if (user != null)
                {
                    if (_core.Authorization.CurrentUser?.CardId == e.CardId) return;
                    await _core.Authorization.InitializeAsync(user);
                    _core.Log.AddLog($"[CardReader] 登入: {user.Name}", LogLevel.Success);
                    return;
                }

                var emp = await _erpApiService.GetEmpInfoByCardAsync(e.CardId);
                if (emp == null)
                {
                    _core.Log.AddLog($"[CardReader] 未識別卡號: {e.CardId}", LogLevel.Warning);
                    return;
                }

                var existingUser = snapshot.FirstOrDefault(u => u.UserId == emp.EmpNo);
                if (existingUser == null)
                    await HandleAddNewEmployeeAsync(emp, e.CardId);
                else
                    await HandleUpdateCardIdAsync(existingUser, e.CardId);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog($"[CardReader] 處理失敗: {ex.Message}", LogLevel.Error);
                _core.Log.AddErrorLog($"[OnCardRead] {ex.Message}");
            }
        }

        private async Task HandleAddNewEmployeeAsync(EmpInfoDto emp, string cardId)
        {
            var msg = $"查詢到 ERP 員工資訊\n\n員工編號：{emp.EmpNo}\n姓名：{emp.Name}\n卡號：{cardId}\n\n" +
                $"是否新增至系統？\n（預設訪客權限，密碼 0000）";

            bool confirmed = await Application.Current.Dispatcher.InvokeAsync(() =>
                _dialog.ShowConfirm(msg));

            if (!confirmed) { _core.CardReader.ResetLastCard(); return; }

            var dto = new EmployeeFormDto
            {
                UserId = emp.EmpNo,
                Name = emp.Name,
                CardId = cardId,
                Password = "0000",
                RoleId = 1
            };
            await _core.Data.AddEmployeeAsync(dto);
            var newList = await _core.Data.GetUsersAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => _commonLists.UsersList = newList);
            _core.Log.AddLog($"[CardReader] 已新增員工: {emp.Name}，請重新刷卡登入", LogLevel.Success);
            _core.CardReader.ResetLastCard();
        }

        private async Task HandleUpdateCardIdAsync(UserInfo existingUser, string cardId)
        {
            var msg = $"系統已有此員工\n\n員工編號：{existingUser.UserId}\n姓名：{existingUser.Name}\n\n新卡號：{cardId}\n\n" +
                $"是否更新卡號至資料庫？";

            bool confirmed = await Application.Current.Dispatcher.InvokeAsync(() =>
                _dialog.ShowConfirm(msg));

            if (!confirmed) { _core.CardReader.ResetLastCard(); return; }

            var dto = new EmployeeFormDto
            {
                Id = existingUser.Id,
                UserId = existingUser.UserId,
                Name = existingUser.Name,
                CardId = cardId,
                RoleId = existingUser.RoleId,
                Email = existingUser.Email,
                DepartmentId = existingUser.DepartmentId
            };
            await _core.Data.UpdateEmployeeAsync(dto);
            await Application.Current.Dispatcher.InvokeAsync(() => existingUser.CardId = cardId);
            _core.Log.AddLog($"[CardReader] 更新卡號: {existingUser.Name}，請重新刷卡登入", LogLevel.Success);
            _core.CardReader.ResetLastCard();
        }
    }
}
