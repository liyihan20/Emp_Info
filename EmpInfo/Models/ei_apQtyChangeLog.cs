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
    
    public partial class ei_apQtyChangeLog
    {
        public int id { get; set; }
        public string sys_no { get; set; }
        public Nullable<int> entry_no { get; set; }
        public string op_name { get; set; }
        public Nullable<System.DateTime> op_date { get; set; }
        public Nullable<decimal> old_qty { get; set; }
        public Nullable<decimal> new_qy { get; set; }
    }
}