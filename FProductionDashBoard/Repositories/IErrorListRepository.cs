using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IErrorListRepository : IRepository<ErrorList, MesDbContext>
    {
        public Task AddErrorAsync(ErrorList newError, List<ErrorTranslation> errortranslations);
        public Task UpdateTranslationAsync(string errorCode, string languageCode, string newMessage);
        public Task DeleteTranslationAsync(string errorCode, string languageCode);
        public Task DeleteErrorAsync(string errorCode);

        public Task<string?> GetMessageAsync(string errorCode, string languageCode);
        public Task<List<(string ErrorCode, string Message, string Category)>> GetMessagesAsync(string languageCode);
        public Task<List<(string ErrorCode, string Message, string Category)>> GetMessagesWithOtherAsync(string languageCode);
        public Task<List<(string LanguageCode, string Message)>> GetTranslationsAsync(string errorCode);
        public Task<List<(string ErrorCode, List<(string LanguageCode, string Message)>)>> GetAllErrorsWithTranslationsAsync();
    }
}
