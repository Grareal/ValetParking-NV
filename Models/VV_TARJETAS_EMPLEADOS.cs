using System.ComponentModel.DataAnnotations;

namespace AppValetParking.Models
{
    public class VV_TARJETAS_EMPLEADOS
    {
        public string? c_fname { get; set; }
        public string? c_lname { get; set; }
        public string? c_mname { get; set; }
        public string? c_nick_name { get; set; }
        public int? c_company_id { get; set; }
        public string? company_name { get; set; }
        public string? ID_ICLASS { get; set; }
        public string? ID_MIFARE { get; set; } 
         public string? emp { get; set; }
        public string? clavenomina { get; set; }     
     }
}
