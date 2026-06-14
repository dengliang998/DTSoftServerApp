using DTSoft.Core.Common;
using DTSoft.Core.DbContexts;
using DTSoft.Models.Entities;
using DTSoft.Models.Parameter.Attachment;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace DTSoft.AppService.Attachment;

public class AttachmentApp(SysDbContext dbContext, DtSoftHelper dtSoftHelper, ConfigHelper configHelper, UserCacheHelper userCacheHelper)
{
    public AttachmentInfo CreateFile(BaseFileParameter objFile)
    {
        var attachment = new AttachmentInfo();
            
        if (objFile.Files!.Length > 0)
        {
            if (!Directory.Exists(objFile.Path))
            {
                Directory.CreateDirectory(objFile.Path);
            }
                
            string fileName = objFile.Files.FileName;
            string ext = fileName[fileName.LastIndexOf('.')..];
            // 文件名称使用 GUID 生成
            string fileId = Guid.NewGuid().ToString();
                
            using FileStream filesStream = File.Create(objFile.Path + "/" + fileId + ext);
            objFile.Files.CopyTo(filesStream);
    
            attachment.FileName = fileName;
            attachment.FileFullName = fileId + ext;
            attachment.FileId = fileId;
            attachment.Ext = ext;
        }
            
        return attachment;
    }
    /// <summary>
    /// 保存文件
    /// </summary>
    public async Task<JsonObject> Save(FileUploadApi objFile, string userAcc)
    {
        string filePath = Path.Combine(configHelper.AttachmentPath, DateTime.Now.Year + DateTime.Now.Month.ToString("00") + DateTime.Now.Day.ToString("00"));
        var attachment = CreateFile(new BaseFileParameter() { Files = objFile.Files, Path = filePath });
        
        if (!attachment.Success)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "未读取到文件"
            };
        }
        
        // 保存文件信息到数据库
        var attachments = new SysAttachments
        {
            ItemId = YitterHelper.NewId(),
            FileName = Convert.ToString(attachment.FileName),
            FileId = Convert.ToString(attachment.FileId),
            Size = objFile.Files!.Length,
            FilePath = filePath,
            CreateUser = userAcc,
            CreateDate = DateTime.Now.ToCstTime(),
            Ext = Convert.ToString(attachment.Ext)
        };

        var item = new JsonObject
        {
            ["FilePath"] = $"{filePath}/{attachment.FileFullName}",
            ["FileName"] = attachments.FileName,
            ["FileID"] = attachments.FileId,
            ["Ext"] = attachments.Ext,
            ["Size"] = objFile.Files.Length
        };

        dbContext.Attachments!.Add(attachments);
        await dbContext.SaveChangesAsync();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["data"] = new JsonArray(item)
        };
    }

    /// <summary>
    /// 保存文件-分片处理
    /// </summary>
    /// <param name="objFile"></param>
    /// <param name="userAcc"></param>
    /// <returns></returns>
    public async Task<JsonObject> Saves(FileUploadInfo objFile, string userAcc)
    {
        JsonObject rv = new()
        {
            ["StateCode"] = 0,
            ["success"] = true
        };

        string attPath = Path.Combine(configHelper.AttachmentPath, DateTime.Now.Year + DateTime.Now.Month.ToString("00") + DateTime.Now.Day.ToString("00"));
        Directory.CreateDirectory(attPath);
        // 分片存储位置
        string temporary = Path.Combine(attPath, "temp");
        
        // 保存分片文件
        if (!Directory.Exists(temporary))
            Directory.CreateDirectory(temporary);
        
        string filePath = Path.Combine(temporary, objFile.Index.ToString());
        if (!Convert.IsDBNull(objFile.File))
        {
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await objFile.File!.CopyToAsync(fs);
        }
        
        // 合并分片文件
        bool mergeOk = false;
        JsonObject? res = null;
        
        if (objFile.Total == objFile.Index)
        {
            res = await FileMerge(temporary, attPath, objFile.FileName, userAcc);
            mergeOk = (bool)res["ok"]!;
        }

        return new JsonObject
        {
            ["number"] = objFile.Index,
            ["MergeOk"] = mergeOk,
            ["FileInfo"] = mergeOk ? res : null
        };
    }

    /// <summary>
    /// 分片文件合并
    /// </summary>
    private async Task<JsonObject> FileMerge(string temporary, string attPath, string fileName, string userAcc)
    {
        string fileExt = Path.GetExtension(fileName); // 获取文件后缀
        var files = Directory.GetFiles(temporary) // 获得下面的所有文件
            .OrderBy(part =>
            {
                var name = Path.GetFileName(part);
                return int.TryParse(name, out var idx) ? idx : int.MaxValue;
            })
            .ThenBy(part => part, StringComparer.Ordinal);
    
        string fileId = Guid.NewGuid().ToString();
        var outputPath = Path.Combine(attPath, fileId + fileExt);
        await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            
        foreach (var part in files)
        {
            await using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            await input.CopyToAsync(output);
            File.Delete(part); // 删除分块
        }
            
        Directory.Delete(temporary); // 删除文件夹

        var size = output.Length;
            
        var att = new SysAttachments
        {
            ItemId = YitterHelper.NewId(),
            FileName = fileName,
            FileId = fileId,
            Size = size,
            FilePath = attPath,
            CreateUser = userAcc,
            CreateDate = DateTime.Now.ToCstTime(),
            Ext = fileExt
        };
            
        dbContext.Attachments!.Add(att);
        await dbContext.SaveChangesAsync();
            
        return new JsonObject
        {
            ["FilePath"] = outputPath,
            ["FileName"] = fileName,
            ["FileID"] = fileId,
            ["Ext"] = fileExt,
            ["Size"] = att.Size,
            ["ok"] = true
        };
    }

    /// <summary>
    /// 文件下载
    /// </summary>
    /// <param name="fileId"></param>
    /// <returns></returns>
    public JsonObject Download(string fileId)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "FileID 不能为空"
            };
        }
    
        var data = dbContext.Attachments!.Where(b => b.FileId == fileId);
        if (!data.Any())
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "FileID 不存在"
            };
        }
    
        var attachments = data.FirstOrDefault();
        string filePath = Path.Combine(attachments!.FilePath!, attachments.FileId + attachments.Ext);
            
        if (File.Exists(filePath))
        {
            return new JsonObject
            {
                ["success"] = true,
                ["StateCode"] = 0,
                ["FilePath"] = filePath,
                ["FileName"] = attachments.FileName
            };
        }
        else
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "文件不存在或已被删除"
            };
        }
    }

    /// <summary>
    /// 获取文件列表
    /// </summary>
    public async Task<JsonObject> GetFileListAsync(FileParameter obj)
    {
        var baseQuery = dbContext.Attachments!.AsNoTracking().AsQueryable();
        var total = await baseQuery.CountAsync();

        // 先获取附件数据，再手动关联用户显示名
        IQueryable<SysAttachments> attachments = baseQuery.OrderByDescending(o => o.ItemId);

        // 应用筛选条件
        if (!string.IsNullOrEmpty(obj.Keyword))
        {
            attachments = attachments.Where(b => b.FileId == obj.Keyword || b.FileName!.Contains(obj.Keyword));
            total = 1;
        }

        // 分页处理
        var pagedAttachments = await attachments
            .Skip(obj.PageSize * (obj.PageNum - 1))
            .Take(obj.PageSize)
            .ToListAsync();

        // 提取所有需要的用户账号
        var userAccounts = pagedAttachments.Select(a => a.CreateUser).Distinct().ToList();
        var users = await userCacheHelper.GetUsersByAccountsAsync(userAccounts);
        var userLookup = users
            .Where(u => !string.IsNullOrEmpty(u.Account))
            .GroupBy(u => u.Account!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName ?? g.Key, StringComparer.OrdinalIgnoreCase);

        var children = new JsonArray();
        foreach (var attachment in pagedAttachments)
        {
            var item = new JsonObject
            {
                ["FileID"] = attachment.FileId,
                ["FileName"] = attachment.FileName,
                ["Size"] = Math.Round(((Convert.ToDecimal(attachment.Size) / 1000) / 1000), 2, MidpointRounding.AwayFromZero),
                ["FilePath"] = attachment.FilePath,
                ["Ext"] = attachment.Ext
            };

            // 从缓存中查找对应的用户显示名
            if (!string.IsNullOrEmpty(attachment.CreateUser) && userLookup.TryGetValue(attachment.CreateUser, out var displayName))
            {
                item["CreateUser"] = displayName;
            }
            else
            {
                item["CreateUser"] = attachment.CreateUser;
            }
            item["CreateDate"] = attachment.CreateDate.ToString("yyyy-MM-dd HH:mm:ss");
            
            children.Add(item);
        }

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Total"] = total,
            ["data"] = children
        };
    }

    public async Task<JsonObject> RemoveFileAsync(string fileId, string loginUserAcc)
    {
        // 权限验证
        if (!dtSoftHelper.IsAdmin(loginUserAcc))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "该账号没有删除权限"
            };
        }

        var data = dbContext.Attachments!.Where(b => b.FileId == fileId);
        if (!data.Any())
        {
            return new JsonObject
            {
                ["success"] = false,
                ["StateCode"] = 0,
                ["Msg"] = "未找到相关文件信息"
            };
        }

        var attachments = data.FirstOrDefault();
        string path = Path.Combine(attachments!.FilePath!, attachments.FileId + attachments.Ext);
        
        // 删除文件
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        
        // 删除数据库记录
        dbContext.Attachments!.Remove(attachments);
        await dbContext.SaveChangesAsync();

        return new JsonObject
        {
            ["success"] = true,
            ["StateCode"] = 0,
            ["Msg"] = "删除成功"
        };
    }
}
