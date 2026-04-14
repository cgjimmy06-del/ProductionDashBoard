using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class ErrorListRepository : Repository<ErrorList, MesDbContext>, IErrorListRepository
    {

        public ErrorListRepository(MesDbContext context) : base(context)
        {
        }

        public async Task AddErrorAsync(ErrorList newError, List<ErrorTranslation> errortranslations)
        {
            var existingError = await _context.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == newError.ErrorCode);

            if (existingError == null)
            {
                // 不存在，新增 ErrorList 與翻譯
                newError.Translations = errortranslations;
                _context.ErrorLists.Add(newError);
            }
            else
            {
                // 已存在 ErrorCode，檢查翻譯
                foreach (var translation in errortranslations)
                {
                    var existsTranslation = existingError.Translations
                        .FirstOrDefault(t => t.LanguageCode == translation.LanguageCode);

                    if (existsTranslation != null) continue;
                    else
                    {
                        // 翻譯不存在 → 新增翻譯
                        existingError.Translations.Add(new ErrorTranslation
                        {
                            LanguageCode = translation.LanguageCode,
                            Message = translation.Message
                        });
                    }
                }
                _context.ErrorLists.Update(existingError);
            }

            await _context.SaveChangesAsync();
        }
        public async Task UpdateTranslationAsync(string errorCode, string languageCode, string newMessage)
        {
            // 先查詢指定 errorCode 的 ErrorList 與翻譯
            var error = await _context.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == errorCode);

            if (error == null)
            {
                throw new InvalidOperationException($"ErrorCode {errorCode} 不存在");
            }

            // 找到指定語言的翻譯
            var translation = error.Translations
                .FirstOrDefault(t => t.LanguageCode == languageCode);

            if (translation == null)
            {
                throw new InvalidOperationException(
                    $"ErrorCode {errorCode} 的語言 {languageCode} 翻譯不存在");
            }

            // 更新訊息
            translation.Message = newMessage;
            translation.UpdateAt = DateTime.Now;

            _context.ErrorTranslations.Update(translation);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteTranslationAsync(string errorCode, string languageCode)
        {
            var error = await _context.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == errorCode);

            if (error == null)
            {
                throw new InvalidOperationException($"ErrorCode {errorCode} 不存在");
            }

            var translation = error.Translations
                .FirstOrDefault(t => t.LanguageCode == languageCode);

            if (translation == null)
            {
                throw new InvalidOperationException(
                    $"ErrorCode {errorCode} 的語言 {languageCode} 翻譯不存在");
            }

            _context.ErrorTranslations.Remove(translation);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteErrorAsync(string errorCode)
        {
            var error = await _context.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == errorCode);

            if (error == null)
            {
                throw new InvalidOperationException($"ErrorCode {errorCode} 不存在");
            }

            // EF Core 會自動刪除子集合 (Translations)，前提是有設定外鍵關聯的 Cascade Delete
            _context.ErrorLists.Remove(error);
            await _context.SaveChangesAsync();
        }

        // 查詢單一錯誤訊息 (指定 代碼及語言)
        public async Task<string?> GetMessageAsync(string errorCode, string languageCode)
        {
            return await _context.ErrorLists
                        .Where(e => e.ErrorCode == errorCode)
                        .SelectMany(e => e.Translations)
                        .Where(t => t.LanguageCode == languageCode)
                        .Select(t => t.Message)
                        .FirstOrDefaultAsync();
        }
        // 查詢所有錯誤訊息 (指定 語言)
        public async Task<List<(string ErrorCode, string Message)>> GetMessagesAsync(string languageCode)
        {
            return await _context.ErrorLists
                .Select(e => new
                {
                    e.ErrorCode,
                    Translation = e.Translations.FirstOrDefault(t => t.LanguageCode == languageCode)
                })
            .Where(x => x.Translation != null)
            .Select(x => new ValueTuple<string, string>(x.ErrorCode, x.Translation!.Message ?? "Unknown!"))
            .ToListAsync();
        }
        // 查詢所有錯誤訊息 (指定 語言，將OTHER排至最後)
        public async Task<List<(string ErrorCode, string Message)>> GetMessagesWithOtherAsync(string languageCode)
        {
            var results = await _context.ErrorLists
                .Select(e => new
                {
                    e.ErrorCode,
                    Translation = e.Translations.FirstOrDefault(t => t.LanguageCode == languageCode)
                })
                .Where(x => x.Translation != null)
                .Select(x => new ValueTuple<string, string>(x.ErrorCode, x.Translation!.Message ?? "Unknown!"))
                .ToListAsync();

            // 排序：先把不是 OTHER 的排前面，OTHER 永遠在最後
            return results
                .OrderBy(r => r.Item1 == "OTHER" ? 1 : 0)
                .ThenBy(r => r.Item1) // 其他錯誤代碼依字母排序 (可選)
                .ToList();
        }
        // 查詢錯誤代碼所有翻譯 (指定 代碼)
        public async Task<List<(string LanguageCode, string Message)>> GetTranslationsAsync(string errorCode)
        {
            return await _context.ErrorLists
                .Where(e => e.ErrorCode == errorCode)
                .SelectMany(e => e.Translations)
                .Select(t => new ValueTuple<string, string>(t.LanguageCode, t.Message ?? ""))
                .ToListAsync();
        }
        // 查詢所有錯誤代碼與翻譯
        public async Task<List<(string ErrorCode, List<(string LanguageCode, string Message)>)>> GetAllErrorsWithTranslationsAsync()
        {
            return await _context.ErrorLists
                .Include(e => e.Translations)
                .Select(e => new ValueTuple<string, List<(string, string)>>(
                    e.ErrorCode,
                    e.Translations
                        .Select(t => new ValueTuple<string, string>(t.LanguageCode, t.Message ?? ""))
                        .ToList()
                ))
                .ToListAsync();
        }
        
    }
}
