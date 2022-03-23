using Infrastructure.Attribute;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using ZR.Model.Models;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysFileService : IBaseService<SysFile>
    {
        Task<long> InsertFile(SysFile file);

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="fileDir"></param>
        /// <param name="fileName"></param>
        /// <param name="formFile"></param>
        /// <param name="rootPath"></param>
        /// <param name="userName"></param>
        /// <returns>文件对象</returns>
        Task<SysFile> SaveFileLocal(string rootPath, string fileName, string fileDir, string userName, IFormFile formFile);
        (bool, string, string) SaveFile(string picdir, IFormFile formFile, string customFileName, string bucketName);
        /// <summary>
        /// 按时间来创建文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <returns>eg: 2020/11/3</returns>
        string GetdirPath(string path = "");

        /// <summary>
        /// 取文件名的MD5值(16位)
        /// </summary>
        /// <param name="name">文件名，不包括扩展名</param>
        /// <returns></returns>
        string HashFileName(string str = null);
    }
}
