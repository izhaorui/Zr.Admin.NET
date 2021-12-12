using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OfficeOpenXml;
namespace ZR.Common
{
    public class ExcelHelper<T> where T : new()
    {
        /// <summary>
        /// 导入数据
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static IEnumerable<T> ImportData(Stream stream)
        {
            using ExcelPackage package = new(stream);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            ExcelWorksheet worksheet = package.Workbook.Worksheets[0];//读取第1个sheet
            //获取表格的列数和行数

            int colStart = worksheet.Dimension.Start.Column;
            int colEnd = worksheet.Dimension.End.Column;
            int rowStart = worksheet.Dimension.Start.Row;
            int rowEnd = worksheet.Dimension.End.Row;
            //int rowCount = worksheet.Dimension.Rows;
            //int ColCount = worksheet.Dimension.Columns;

            List<T> resultList = new();
            List<PropertyInfo> propertyInfos = new();// new(typeof(T).GetProperties());
            Dictionary<string, int> dictHeader = new();
            for (int i = colStart; i < colEnd; i++)
            {
                var name = worksheet.Cells[rowStart, i].Value.ToString();
                dictHeader[name] = i;

                PropertyInfo propertyInfo = MapPropertyInfo(name);
                if (propertyInfo != null)
                {
                    propertyInfos.Add(propertyInfo);
                }
            }
            for (int row = rowStart + 1; row <= rowEnd; row++)
            {
                T result = new();

                foreach (PropertyInfo p in propertyInfos)
                {
                    try
                    {
                        ExcelRange cell = worksheet.Cells[row, dictHeader[p.Name]];
                        if (cell.Value == null)
                        {
                            continue;
                        }
                        switch (p.PropertyType.Name.ToLower())
                        {
                            case "string":
                                p.SetValue(result, cell.GetValue<string>());
                                break;
                            case "int16":
                                p.SetValue(result, cell.GetValue<short>()); break;
                            case "int32":
                                p.SetValue(result, cell.GetValue<int>()); break;
                            case "int64":
                                p.SetValue(result, cell.GetValue<long>()); break;
                            case "decimal":
                                p.SetValue(result, cell.GetValue<decimal>());
                                break;
                            case "double":
                                p.SetValue(result, cell.GetValue<double>()); break;
                            case "datetime":
                                p.SetValue(result, cell.GetValue<DateTime>()); break;
                            case "boolean":
                                p.SetValue(result, cell.GetValue<bool>()); break;
                            case "char":
                                p.SetValue(result, cell.GetValue<string>()); break;
                            default:
                                break;
                        }
                    }
                    catch (KeyNotFoundException ex)
                    {
                        Console.WriteLine("未找到该列将继续循环，" + ex.Message);
                        continue;
                    }
                }
                resultList.Add(result);
            }

            return resultList;
        }

        /// <summary>
        /// 查找Excel列名对应的实体属性
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public static PropertyInfo MapPropertyInfo(string columnName)
        {
            PropertyInfo[] propertyList = GetProperties(typeof(T));
            PropertyInfo propertyInfo = propertyList.Where(p => p.Name == columnName).FirstOrDefault();
            if (propertyInfo != null)
            {
                return propertyInfo;
            }
            else
            {
                foreach (PropertyInfo tempPropertyInfo in propertyList)
                {
                    System.ComponentModel.DescriptionAttribute[] attributes = (System.ComponentModel.DescriptionAttribute[])tempPropertyInfo.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                    if (attributes.Length > 0)
                    {
                        if (attributes[0].Description == columnName)
                        {
                            return tempPropertyInfo;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 得到类里面的属性集合
        /// </summary>
        /// <param name="type"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        public static PropertyInfo[] GetProperties(Type type, string[] columns = null)
        {
            PropertyInfo[] properties = null;
            properties = type.GetProperties();

            if (columns != null && columns.Length > 0)
            {
                //  按columns顺序返回属性
                var columnPropertyList = new List<PropertyInfo>();
                foreach (var column in columns)
                {
                    var columnProperty = properties.Where(p => p.Name == column).FirstOrDefault();
                    if (columnProperty != null)
                    {
                        columnPropertyList.Add(columnProperty);
                    }
                }
                return columnPropertyList.ToArray();
            }
            else
            {
                return properties;
            }
        }
    }
}
