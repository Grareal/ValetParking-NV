using System.ComponentModel.DataAnnotations.Schema;

namespace AppValetParking.Models
{
    public class Reserva
{
        [NotMapped]
        public List<string> NombresAcompanantes { get; set; } = new List<string>();
        [NotMapped]
        public string AcompanantesTexto { get; set; } = string.Empty;
        public string? h_status { get; set; }
        public string? h_res_cve { get; set; }
        public string? h_res_fec { get; set; }      // varchar, no DateTime
        public string? h_res_hra { get; set; }      // varchar, no TimeSpan
        public string? h_cod_gpo { get; set; }
        public string? h_nom { get; set; }
        public string? h_fec_lld { get; set; }      // varchar, no DateTime
        public string? h_fec_sda { get; set; }      // varchar, no DateTime
        public double? h_num_adu { get; set; }
        public double? h_num_men { get; set; }
        public double? h_num_per { get; set; }
        public string? h_num_hab { get; set; }      // varchar
        public string? h_tpo_hab { get; set; }
        public string? h_for_pgo { get; set; }
        public string? h_cod_reserva { get; set; }
        public string? h_seg_mer { get; set; }
        public string? h_tpo_hsp { get; set; }
        public string? h_apellido_p { get; set; }
        public string? m_msg0 { get; set; }
        public string? Hotel { get; set; }

    }

}