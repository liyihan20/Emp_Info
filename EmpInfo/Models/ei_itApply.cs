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
    
    public partial class ei_itApply
    {
        public int id { get; set; }
        public string sys_no { get; set; }
        public string applier_num { get; set; }
        public string applier_name { get; set; }
        public Nullable<System.DateTime> apply_time { get; set; }
        public string dep_name { get; set; }
        public string emp_position { get; set; }
        public Nullable<int> priority { get; set; }
        public string computer_number { get; set; }
        public string ip_addr { get; set; }
        public string computer_name { get; set; }
        public string faulty_items { get; set; }
        public string applier_comment { get; set; }
        public Nullable<int> estimate_it_fee { get; set; }
        public string fixed_items { get; set; }
        public Nullable<int> real_it_fee { get; set; }
        public string it_comment { get; set; }
        public string repair_way { get; set; }
        public Nullable<int> evaluation_score { get; set; }
        public string evaluation_comment { get; set; }
        public string repair_man { get; set; }
        public Nullable<System.DateTime> repair_time { get; set; }
        public string applier_phone { get; set; }
        public string dep_charger_no { get; set; }
        public string dep_charger_name { get; set; }
        public Nullable<System.DateTime> evaluation_time { get; set; }
    }
}
