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
    
    public partial class ei_DEApplyEntry
    {
        public int id { get; set; }
        public int de_id { get; set; }
        public string catalog { get; set; }
        public string subject { get; set; }
        public string name { get; set; }
        public string summary { get; set; }
        public string unit_name { get; set; }
        public Nullable<decimal> qty { get; set; }
        public Nullable<decimal> unit_price { get; set; }
        public Nullable<decimal> total { get; set; }
        public Nullable<decimal> tax_rate { get; set; }
        public Nullable<decimal> total_with_tax { get; set; }
        public string comment { get; set; }
        public string supplier_name { get; set; }
        public Nullable<System.DateTime> clear_date { get; set; }
    
        public virtual ei_DEApply ei_DEApply { get; set; }
    }
}
