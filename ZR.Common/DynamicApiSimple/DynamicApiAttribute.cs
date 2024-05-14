using System;

namespace ZR.Common.DynamicApiSimple
{
    public class DynamicApiAttribute:Attribute
    {
        public string Name;
        public string Order;
        public string Description;
        public DynamicApiAttribute()
        {
            
        }
        public DynamicApiAttribute(string _name,string _order,string _description)
        {
            Name = _name;
            Order = _order; 
            Description = _description;
        }
    }
}
