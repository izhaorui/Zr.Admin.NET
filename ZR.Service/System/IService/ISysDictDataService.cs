using Infrastructure.Model;
using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysDictDataService
    {
        public PagedInfo<SysDictData> SelectDictDataList(SysDictData dictData, PagerInfo pagerInfo);
        public List<SysDictData> SelectDictDataByType(string dictType);
        public List<SysDictData> SelectDictDataByTypes(string[] dictTypes);
        public SysDictData SelectDictDataById(long dictCode);
        public long InsertDictData(SysDictData dict);
        public long UpdateDictData(SysDictData dict);
        public int DeleteDictDataByIds(long[] dictCodes);
    }
}
