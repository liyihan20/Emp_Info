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
    
    public partial class ei_xcProduct
    {
        public int id { get; set; }
        public string sys_no { get; set; }
        public string product_no { get; set; }
        public string product_name { get; set; }
        public string product_model { get; set; }
        public Nullable<decimal> qty { get; set; }
        public string unit_name { get; set; }
        public Nullable<decimal> unit_price { get; set; }
        public Nullable<decimal> total_price { get; set; }
        public Nullable<int> entry_id { get; set; }
    }
}