namespace AppValetParking.Models
{ 

public class ReservationAllView
{
        public string? GUEST_NAME { get; set; }
        public string? CONFIRMATION_NO { get; set; }
        /// El TSW. La columna ya existía en REP_RESERVATION_ALL_VIEW; solo no
        /// estaba mapeada, por eso no se podía buscar por ella.
        public string? EXTERNAL_REFERENCE { get; set; }
        public string? ACCOMPANYING_NAMES { get; set; }
        public string? ROOM { get; set; }
        public string? ROOM_CLASS { get; set; }
        public double? RESV_NAME_ID { get; set; }
    }
}