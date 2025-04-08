using System;
using System.Text.Json.Serialization;

namespace ProducerPoc.Models
{
    public class IndustriaEvento
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("Origem")]
        public int Origem { get; set; }  // 0 - Indústria

        [JsonPropertyName("Processo")]
        public string Processo { get; set; } = default!;  // "Produção"

        [JsonPropertyName("Canal")]
        public string Canal { get; set; } = default!;     // "Geral"

        [JsonPropertyName("Etapa")]
        public string Etapa { get; set; } = default!;

        [JsonPropertyName("IdentificadorExterno")]
        public string IdentificadorExterno { get; set; } = default!; // "1000000", etc.

        [JsonPropertyName("Status")]
        public int Status { get; set; }  // 0 - início, 1 - fim

        [JsonPropertyName("Operador")]
        public Operador Operador { get; set; } = default!;

        [JsonPropertyName("QuantidadeItens")]
        public int QuantidadeItens { get; set; }

        [JsonPropertyName("TipoEtapaNoCliclo")]
        public int TipoEtapaNoCliclo { get; set; }

        [JsonPropertyName("DataHoraEvento")]
        public DateTime DataHoraEvento { get; set; }
    }

    public class Operador
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("Nome")]
        public string Nome { get; set; } = default!;
    }
}
