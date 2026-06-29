using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppValetParking.Models
{
    [Table("RESERVATION_NAME_CLOUD", Schema = "dbo")]
    public class ReservationSearch
    {
        [Key]
        public int RESV_NAME_ID { get; set; }

        public string? RESORT { get; set; }
        public string? CONFIRMATION_NO { get; set; }
        public string? EXTERNAL_REFERENCE { get; set; }


        public string? SGUEST_FIRSTNAME { get; set; }
        public string? SGUEST_NAME { get; set; }

        public string? ROOM { get; set; }
        public string? ROOM_CLASS_DESCRIPTION { get; set; }
        public string? ROOM_CLASS { get; set; }


        public string? MARKET_CODE { get; set; }
        public string? VIP { get; set; }
    }
}