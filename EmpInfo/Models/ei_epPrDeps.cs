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
    
    public partial class ei_epPrDeps
    {
        public int id { get; set; }
        public string dep_num { get; set; }
        public string dep_name { get; set; }
        public string bus_dep_name { get; set; }
        public string charger_num { get; set; }
        public string charger_name { get; set; }
        public string chief_num { get; set; }
        public string chief_name { get; set; }
        public string minister_num { get; set; }
        public string minister_name { get; set; }
        public Nullable<int> eq_dep_id { get; set; }
        public string creater_num { get; set; }
        public string creater_name { get; set; }
        public Nullable<System.DateTime> create_date { get; set; }
    
        public virtual ei_epEqDeps ei_epEqDeps { get; set; }
    }
}