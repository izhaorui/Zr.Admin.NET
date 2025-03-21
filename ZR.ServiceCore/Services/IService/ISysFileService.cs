using Microsoft.AspNetCore.Http;
using ZR.Model.Dto;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    public interface ISysFileService : IBaseService<SysFile>
    {
        Task<long> InsertFile(SysFile file);

        /// <summary>
        /// 上传文件到本地
        /// </summary>
        /// <param name="formFile"></param>
        /// <param name="rootPath"></param>
        /// <param name="userName"></param>
        /// <param name="dto"></param>
        /// <returns>文件对象</returns>
        Task<SysFile> SaveFileToLocal(string rootPath, UploadDto dto, string userName, IFormFile formFile);
        
        /// <summary>
        /// 上传文件到阿里云
        /// </summary>
        /// <param name="file"></param>
        /// <param name="dto"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        Task<SysFile> SaveFileToAliyun(SysFile file, UploadDto dto, IFormFile formFile);
        
        /// <summary>
        /// 按时间来创建文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <param name="byTimeStore"></param>
        /// <returns>eg: 2020/11/3</returns>
        string GetdirPath(string path = "", bool byTimeStore = true);

        /// <summary>
        /// 取文件名的MD5值(16位)
        /// </summary>
        /// <param name="str">文件名，不包括扩展名</param>
        /// <returns></returns>
        string HashFileName(string str = null);

        int UpdateFile(SysFile parm);
    }
}
