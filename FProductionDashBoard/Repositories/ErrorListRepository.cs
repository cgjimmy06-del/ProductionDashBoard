using FProductionDashBoard.Dtos;
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
        public ErrorListRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task AddErrorAsync(ErrorList newError, List<ErrorTranslation> errortranslations)
        {
            await using var ctx = _factory.CreateDbContext();
            var existingError = await ctx.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == newError.ErrorCode);

            if (existingError == null)
            {
                newError.Translations = errortranslations;
                ctx.ErrorLists.Add(newError);
            }
            else
            {
                foreach (var translation in errortranslations)
                {
                    var existsTranslation = existingError.Translations
                        .FirstOrDefault(t => t.LanguageCode == translation.LanguageCode);

                    if (existsTranslation != null) continue;
                    else
                    {
                        existingError.Translations.Add(new ErrorTranslation
                        {
                            LanguageCode = translation.LanguageCode,
                            Message = translation.Message
                        });
                    }
                }
                ctx.ErrorLists.Update(existingError);
            }

            await ctx.SaveChangesAsync();
        }

        public async Task UpdateTranslationAsync(string errorCode, string languageCode, string newMessage)
        {
            await using var ctx = _factory.CreateDbContext();
            var error = await ctx.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == errorCode);

            if (error == null)
                throw new InvalidOperationException($"ErrorCode {errorCode} 不存在");

            var translation = error.Translations
                .FirstOrDefault(t => t.LanguageCode == languageCode);

            if (translation == null)
                throw new InvalidOperationException($"ErrorCode {errorCode} 的語言 {languageCode} 翻譯不存在");

            translation.Message = newMessage;
            translation.UpdateAt = DateTime.Now;

            ctx.ErrorTranslations.Update(translation);
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteTranslationAsync(string errorCode, string languageCode)
        {
            await using var ctx = _factory.CreateDbContext();
            var error = await ctx.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == errorCode);

            if (error == null)
                throw new InvalidOperationException($"ErrorCode {errorCode} 不存在");

            var translation = error.Translations
                .FirstOrDefault(t => t.LanguageCode == languageCode);

            if (translation == null)
                throw new InvalidOperationException($"ErrorCode {errorCode} 的語言 {languageCode} 翻譯不存在");

            ctx.ErrorTranslations.Remove(translation);
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteErrorAsync(string errorCode)
        {
            await using var ctx = _factory.CreateDbContext();
            var error = await ctx.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorCode == errorCode);

            if (error == null)
                throw new InvalidOperationException($"ErrorCode {errorCode} 不存在");

            ctx.ErrorLists.Remove(error);
            await ctx.SaveChangesAsync();
        }

        public async Task<string?> GetMessageAsync(string errorCode, string languageCode)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ErrorLists
                        .Where(e => e.ErrorCode == errorCode)
                        .SelectMany(e => e.Translations)
                        .Where(t => t.LanguageCode == languageCode)
                        .Select(t => t.Message)
                        .FirstOrDefaultAsync();
        }

        public async Task<List<(string ErrorCode, string Message, int TypeId)>> GetMessagesAsync(string languageCode)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ErrorLists
                .Select(e => new
                {
                    e.ErrorCode,
                    Translation = e.Translations.FirstOrDefault(t => t.LanguageCode == languageCode),
                    e.TypeId
                })
            .Where(x => x.Translation != null)
            .Select(x => new ValueTuple<string, string, int>(x.ErrorCode,
                    x.Translation!.Message ?? "Unknown!", x.TypeId ?? 1))
            .ToListAsync();
        }

        public async Task<List<(string ErrorCode, string Message, int TypeId)>> GetMessagesWithOtherAsync(string languageCode)
        {
            await using var ctx = _factory.CreateDbContext();
            var results = await ctx.ErrorLists
                .Select(e => new
                {
                    e.ErrorCode,
                    Translation = e.Translations.FirstOrDefault(t => t.LanguageCode == languageCode),
                    e.TypeId
                })
                .Where(x => x.Translation != null)
                .Select(x => new ValueTuple<string, string, int>(x.ErrorCode,
                    x.Translation!.Message ?? "Unknown!", x.TypeId ?? 1))
                .ToListAsync();

            return results
                .OrderBy(r => r.Item1 == "OTHER" ? 1 : 0)
                .ThenBy(r => r.Item1)
                .ToList();
        }

        public async Task<List<(string LanguageCode, string Message)>> GetTranslationsAsync(string errorCode)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ErrorLists
                .Where(e => e.ErrorCode == errorCode)
                .SelectMany(e => e.Translations)
                .Select(t => new ValueTuple<string, string>(t.LanguageCode, t.Message ?? ""))
                .ToListAsync();
        }

        public async Task<List<(string ErrorCode, List<(string LanguageCode, string Message)>)>> GetAllErrorsWithTranslationsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ErrorLists
                .Include(e => e.Translations)
                .Select(e => new ValueTuple<string, List<(string, string)>>(
                    e.ErrorCode,
                    e.Translations
                        .Select(t => new ValueTuple<string, string>(t.LanguageCode, t.Message ?? ""))
                        .ToList()
                ))
                .ToListAsync();
        }

        public async Task<List<ErrorList>> GetAllWithTranslationsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ErrorLists
                .Include(e => e.Translations)
                .Include(e => e.Type)
                .ToListAsync();
        }
        public async Task<List<ListType>> GetListTypesAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<ListType>().ToListAsync();
        }

        public async Task UpdateErrorListAsync(ErrorListFormDto dto)
        {
            await using var ctx = _factory.CreateDbContext();
            var entity = await ctx.ErrorLists
                .Include(e => e.Translations)
                .FirstOrDefaultAsync(e => e.ErrorId == dto.Id!.Value)
                ?? throw new InvalidOperationException($"ErrorList id={dto.Id} not found");
            entity.TypeId = dto.TypeId;
            entity.Severity = dto.Severity;
            entity.UpdateAt = DateTime.Now;
            var langMessages = new[] {
                ("zh-TW", dto.MessageZhTw),
                ("en-US", dto.MessageEnUs),
                ("vi-VN", dto.MessageViVn)
            };
            foreach (var (lang, message) in langMessages)
            {
                if (string.IsNullOrWhiteSpace(message)) continue;
                var existing = entity.Translations.FirstOrDefault(t => t.LanguageCode == lang);
                if (existing != null)
                {
                    existing.Message = message;
                    existing.UpdateAt = DateTime.Now;
                }
                else
                {
                    entity.Translations.Add(new ErrorTranslation
                    {
                        ErrorCode = entity.ErrorCode,
                        LanguageCode = lang,
                        Message = message
                    });
                }
            }
            await ctx.SaveChangesAsync();
        }
    }
}
