using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using ProducerPoc.Models;
using System.Collections.Generic;
using System;

public class Program
{
    // Operadores definidos na premissa
    private static readonly List<Operador> _operadores = new List<Operador>
    {
        new Operador { Id = "32987234skjfhdg45", Nome = "Caique Neves" },
        new Operador { Id = "654321214wiuery98", Nome = "Marluce Cabral" },
        new Operador { Id = "664321204wiuerz00", Nome = "Wilson Pereira" },
        new Operador { Id = "775321204wiuerv22", Nome = "Marina Rocha" }
    };

    private static readonly string[] _etapas = new string[]
    {
        "PICKING",
        "SEPARAÇÃO",
        "PRIME",
        "ESTAMPA",
        "BORDADO",
        "BOLSO",
        "PATCH",
        "QUALIDADE",
        "CHECKOUT",
        "PRODUÇÃO EXTERNA",
        "FATURAMENTO"
    };

    public static void Main(string[] args)
    {
        // Ajuste a quantidade de lotes
        int quantidadeLotes = 1000;
        int loteInicial = 1000000;

        // Conexão com RabbitMQ (ajuste se for outro host/porta)
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,  // Padrão
            UserName = "guest",
            Password = "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Declare a exchange e/ou fila a ser usada
        // Para simplificar, vamos enviar direto para uma Fila "industria_eventos_queue"
        channel.QueueDeclare(
            queue: "industria_eventos_queue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var random = new Random();

        // Loop de lotes
        for (int i = 0; i < quantidadeLotes; i++)
        {
            var identificadorExterno = (loteInicial + i).ToString();

            bool DataHoraBaseAtribuida = false;
            DateTime DataHoraBaseInicioLote = DateTime.MinValue;

            // Quantidade itens entre 5 e 100
            int quantidadeItensLote = random.Next(5, 101);

            // Loop de etapas
            foreach (var etapa in _etapas)
            {
                if (!DataHoraBaseAtribuida){
                    DataHoraBaseInicioLote = DateTime.Now
                    .AddDays(random.Next(-10, 0)) // ex: espalhar em últimos 10 dias
                    .AddHours(random.Next(0, 24))
                    .AddMinutes(random.Next(0, 60));

                    DataHoraBaseAtribuida = true;
                }
                
                // Qual TipoEtapaNoCliclo?
                int tipoEtapa = 1;
                if (etapa == "PICKING") tipoEtapa = 0;
                else if (etapa == "FATURAMENTO") tipoEtapa = 2;

                // Seleciona um operador aleatório
                var operador = _operadores[random.Next(_operadores.Count)];

                // Gera data/hora "início"
                var dataHoraInicio = DataHoraBaseInicioLote;

                // Gera data/hora "fim" com diferença 20 a 120 minutos
                var minutosDiff = random.Next(20, 121);
                var dataHoraFim = dataHoraInicio.AddMinutes(minutosDiff);

                DataHoraBaseInicioLote = dataHoraFim.AddMinutes(random.Next(1, 5));

                // 1) Mensagem de Status=0
                var eventoInicio = new IndustriaEvento
                {
                    Id = Guid.NewGuid().ToString(),
                    Origem = 0,  // Indústria
                    Processo = "Producao",
                    Canal = "Geral",
                    Etapa = etapa,
                    IdentificadorExterno = identificadorExterno,
                    Status = 0,  // início
                    Operador = operador,
                    QuantidadeItens = quantidadeItensLote,
                    TipoEtapaNoCliclo = tipoEtapa,
                    DataHoraEvento = dataHoraInicio
                };

                PublishEvento(channel, eventoInicio);

                // 2) Mensagem de Status=1
                var eventoFim = new IndustriaEvento
                {
                    Id = Guid.NewGuid().ToString(),
                    Origem = 0,
                    Processo = "Producao",
                    Canal = "Geral",
                    Etapa = etapa,
                    IdentificadorExterno = identificadorExterno,
                    Status = 1,  // fim
                    Operador = operador,
                    QuantidadeItens = quantidadeItensLote,
                    TipoEtapaNoCliclo = tipoEtapa,
                    DataHoraEvento = dataHoraFim
                };

                PublishEvento(channel, eventoFim);
            }
        }

        Console.WriteLine("Mensagens publicadas com sucesso!");
    }

    private static void PublishEvento(IModel channel, IndustriaEvento evento)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evento));

        channel.BasicPublish(
            exchange: "",                 // vazio = direct/default exchange
            routingKey: "industria_eventos_queue",
            basicProperties: null,
            body: body);
    }
}
