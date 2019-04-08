using System;

namespace EmpInfo.Util
{
    public class BillUtils
    {        
        /// <summary>
        /// 取得流程名
        /// </summary>
        /// <param name="typ"></param>
        /// <returns></returns>
        public string GetBillType(string typ)
        {
            object obj = GetBillSvInstance(typ);
            if (obj == null) return "";

            Type type = obj.GetType();
            return (string)type.GetProperty("BillTypeName").GetValue(obj, null);
        }

        //从流水号获得单据类型
        public string GetBillEnType(string sysNo)
        {
            //先统一取前两位为类型，如有例外再修改
            return sysNo.Substring(0, 2);
        }
        
        /// <summary>
        /// 取得单据实体，返回空对象
        /// </summary>
        /// <param name="billType"></param>
        /// <returns></returns>
        public object GetBillSvInstance(string billType)
        {
            string ty = billType.Length >= 2 ? billType.Substring(0, 2) : "";
            if (!string.IsNullOrEmpty(ty)) {
                Type t = Type.GetType(string.Format("EmpInfo.Services.{0}Sv", ty));
                if (t.IsClass) {
                    return Activator.CreateInstance(t);
                }
            }
            return null;
        }
                
        /// <summary>
        /// 取得单据实体,并且使用sysNo当作构造函数的参数返回实体对象
        /// </summary>
        /// <param name="sysNo"></param>
        /// <returns></returns>
        public object GetBillSvInstanceBySysNo(string sysNo)
        {
            string billType = GetBillEnType(sysNo);
            string ty = billType.Length >= 2 ? billType.Substring(0, 2) : "";
            if (!string.IsNullOrEmpty(ty)) {
                Type t = Type.GetType(string.Format("EmpInfo.Services.{0}Sv", ty));
                if (t.IsClass) {
                    return Activator.CreateInstance(t, new object[] { sysNo });
                }
            }
            return null;
        }
    }
}