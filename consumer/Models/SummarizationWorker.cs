using System;
using System.Timers;
using Microsoft.Data.SqlClient;

namespace ConsumerPoc
{
    public class SummarizationWorker
    {
        private readonly System.Timers.Timer _timer;
                private readonly string _connectionString;

        public SummarizationWorker(string connectionString)
        {
            _connectionString = connectionString;

            // Intervalo de 5 minutos = 300.000 ms
            _timer = new System.Timers.Timer(1_000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true; // dispara novamente após cada intervalo
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                ConsolidateData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SummarizationWorker] Erro na consolidação: {ex.Message}");
            }
        }

        private void ConsolidateData()
        {
            // 1) Inserir (ou atualizar) industria_ciclo:
            ConsolidateIndustriaCiclo();

            // 2) Inserir (ou atualizar) industria_quantidade_etapa:
            ConsolidateIndustriaQuantidadeEtapa();

            // 3) Consolidar tempo médio por etapa (industria_tempo_etapa)...
            ConsolidateIndustriaTempoEtapa();

            Console.WriteLine($"[SummarizationWorker] Consolidação executada em {DateTime.Now}");
        }

        /// <summary>
        /// Exemplo: Identifica todos os identificadores que tenham 
        ///   - um evento de início (status=0, tipoEtapaNoCiclo=0) 
        ///   - e um evento de fim (status=1, tipoEtapaNoCiclo=2).
        /// Cria (INSERT) um registro em industria_ciclo 
        /// se ele ainda não existir.
        /// </summary>
        private void ConsolidateIndustriaCiclo()
        {
            // Podemos usar um INSERT ... SELECT ... HAVING 
            // ou MERGE. Abaixo, um INSERT com HAVING e um NOT EXISTS:
            var sql = @"
                INSERT INTO industria_ciclo
                (
                    identificadorExterno,
                    inicio_ciclo,
                    fim_ciclo,
                    tempo_ciclo_minutos,
                    ano_evento,
                    mes_evento,
                    dia_evento,
                    hora_evento
                )
                SELECT 
                    e.identificadorExterno,
                    MIN(CASE WHEN e.tipo_etapa_no_ciclo=0 AND e.status=0 THEN e.data_hora_evento END) AS inicio_ciclo,
                    MAX(CASE WHEN e.tipo_etapa_no_ciclo=2 AND e.status=1 THEN e.data_hora_evento END) AS fim_ciclo,
                    DATEDIFF(MINUTE,
                        MIN(CASE WHEN e.tipo_etapa_no_ciclo=0 AND e.status=0 THEN e.data_hora_evento END),
                        MAX(CASE WHEN e.tipo_etapa_no_ciclo=2 AND e.status=1 THEN e.data_hora_evento END)
                    ) AS tempo_ciclo_minutos,
                    MAX(e.ano_evento) as ano_evento,
                    MAX(e.mes_evento) as mes_evento,
                    MAX(e.data_evento) as dia_evento,
                    MAX(e.hora_evento) as hora_evento
                FROM industria_evento e
                GROUP BY e.identificadorExterno
                HAVING 
                    -- só insere se houver ao menos um start (status=0,tipo=0)
                    MIN(CASE WHEN e.tipo_etapa_no_ciclo=0 AND e.status=0 THEN e.data_hora_evento END) IS NOT NULL
                    -- e ao menos um fim (status=1,tipo=2)
                    AND MAX(CASE WHEN e.tipo_etapa_no_ciclo=2 AND e.status=1 THEN e.data_hora_evento END) IS NOT NULL
                    -- e não insira se já existe no industria_ciclo
                    AND NOT EXISTS (
                      SELECT 1 
                      FROM industria_ciclo ic 
                      WHERE ic.identificadorExterno = e.identificadorExterno
                    );
            ";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            var rows = cmd.ExecuteNonQuery();
            
            if (rows > 0)
                Console.WriteLine($"[ConsolidateIndustriaCiclo] Inseridos {rows} novos registro(s) em industria_ciclo.");
        }

        /// <summary>
        /// Exemplo: para cada combinação (etapa, ano_evento, mes_evento, data_evento, hora_evento)
        /// em que status=1 (fim de etapa), somamos as quantidades e upsert (Merge) em industria_quantidade_etapa.
        /// </summary>
        private void ConsolidateIndustriaQuantidadeEtapa()
        {
            // Se seu SQL Server suportar MERGE, podemos fazer assim:
            var sql = @"
                MERGE industria_quantidade_etapa AS T
                USING
                (
                    SELECT
                        e.etapa,
                        e.ano_evento,
                        e.mes_evento,
                        e.data_evento,
                        e.hora_evento,
                        SUM(e.quantidade_itens) AS total_itens
                    FROM industria_evento e
                    WHERE e.status = 1  -- só consideramos fim de etapa
                    GROUP BY 
                        e.etapa,
                        e.ano_evento,
                        e.mes_evento,
                        e.data_evento,
                        e.hora_evento
                ) AS S
                ON (
                    T.etapa = S.etapa
                    AND T.ano_evento = S.ano_evento
                    AND T.mes_evento = S.mes_evento
                    AND T.dia_evento = S.data_evento
                    AND T.hora_evento = S.hora_evento
                )
                WHEN MATCHED THEN
                    UPDATE SET T.total_itens = S.total_itens
                WHEN NOT MATCHED THEN
                    INSERT (etapa, ano_evento, mes_evento, dia_evento, hora_evento, total_itens)
                    VALUES (S.etapa, S.ano_evento, S.mes_evento, S.data_evento, S.hora_evento, S.total_itens)
                ;
            ";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            var rows = cmd.ExecuteNonQuery();

            if (rows > 0)
                Console.WriteLine($"[ConsolidateIndustriaQuantidadeEtapa] MERGE afetou {rows} linha(s).");
        }
    
        private void ConsolidateIndustriaTempoEtapa()
        {
            var sql = @"
                MERGE dbo.industria_tempo_etapa AS T
                USING
                (
                    SELECT
                        eInicio.etapa,
                        eInicio.ano_evento,
                        eInicio.mes_evento,
                        eInicio.data_evento,
                        eInicio.hora_evento,
                        CAST(
                            AVG(
                                DATEDIFF(
                                    MINUTE,
                                    eInicio.data_hora_evento,
                                    eFim.data_hora_evento
                                )
                            ) AS INT
                        ) AS tempo_medio_minutos
                    FROM industria_evento eInicio
                    JOIN industria_evento eFim
                        ON eInicio.identificadorExterno = eFim.identificadorExterno
                    AND eInicio.etapa = eFim.etapa
                    AND eInicio.status = 0
                    AND eFim.status = 1
                    AND eInicio.data_hora_evento < eFim.data_hora_evento
                    GROUP BY
                        eInicio.etapa,
                        eInicio.ano_evento,
                        eInicio.mes_evento,
                        eInicio.data_evento,
                        eInicio.hora_evento
                ) AS S
                ON (
                    T.etapa = S.etapa
                    AND T.ano_evento = S.ano_evento
                    AND T.mes_evento = S.mes_evento
                    AND T.dia_evento = S.data_evento
                    AND T.hora_evento = S.hora_evento
                )
                WHEN MATCHED THEN
                    UPDATE SET T.tempo_medio_minutos = S.tempo_medio_minutos
                WHEN NOT MATCHED THEN
                    INSERT (
                        etapa, 
                        ano_evento, 
                        mes_evento, 
                        dia_evento, 
                        hora_evento, 
                        tempo_medio_minutos
                    )
                    VALUES (
                        S.etapa, 
                        S.ano_evento, 
                        S.mes_evento, 
                        S.data_evento, 
                        S.hora_evento, 
                        S.tempo_medio_minutos
                    )
                ;";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            var rows = cmd.ExecuteNonQuery();

            if (rows > 0)
                Console.WriteLine($"[ConsolidateIndustriaTempoEtapa] MERGE afetou {rows} linha(s).");
        }

    }
}
