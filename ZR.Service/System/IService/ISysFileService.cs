using Infrastructure.Attribute;
using Microsoft.AspNetCore.Http;
using ZR.Model.Models;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysFileService : IBaseService<SysFile>
    {
        long InsertFile(SysFile file);

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="picdir"></param>
        /// <param name="formFile"></param>
        /// <returns>结果、地址、文件名</returns>
        (bool, string, string) SaveFile(string picdir, IFormFile formFile);
        (bool, string, string) SaveFile(string picdir, IFormFile formFile, string customFileName);
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
