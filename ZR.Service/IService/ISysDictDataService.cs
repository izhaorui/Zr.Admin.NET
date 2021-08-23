using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model.System;

namespace ZR.Service.IService
{
    public interface ISysDictDataService : IBaseService<SysDictData>
    {
        public List<SysDictData> SelectDictDataList(SysDictData dictData);
        public List<SysDictData> SelectDictDataByType(string dictType);
        public SysDictData SelectDictDataById(long dictCode);
        public long InsertDictData(SysDictData dict);
        public long UpdateDictData(SysDictData dict);
        public int DeleteDictDataByIds(long[] dictCodes);
    }
}
