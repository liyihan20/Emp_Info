//------------------------------------------------------------------------------
// <auto-generated>
//    此代码是根据模板生成的。
//
//    手动更改此文件可能会导致应用程序中发生异常行为。
//    如果重新生成代码，则将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace EmpInfo.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class ei_hhReturnDetail
    {
        public int id { get; set; }
        public Nullable<int> hh_id { get; set; }
        public string moduel { get; set; }
        public Nullable<int> return_qty { get; set; }
        public Nullable<System.DateTime> return_time { get; set; }
        public string ex_company { get; set; }
        public string ex_no { get; set; }
        public string ex_to_name { get; set; }
        public string fetch_name { get; set; }
        public Nullable<System.DateTime> op_time { get; set; }
    
        public virtual ei_hhApply ei_hhApply { get; set; }
    }
}
