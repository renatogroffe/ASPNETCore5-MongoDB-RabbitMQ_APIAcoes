using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using MongoDB.Driver;
using APIAcoes.Models;
using APIAcoes.Documents;

namespace APIAcoes.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AcoesController : ControllerBase
    {
        private readonly ILogger<AcoesController> _logger;
        private readonly IConfiguration _configuration;

        public AcoesController(ILogger<AcoesController> logger,
            [FromServices]IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost]
        [ProducesResponseType(typeof(Resultado), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public Resultado Post(Acao acao)
        {
            CotacaoAcao cotacaoAcao = new ()
            {
                Codigo = acao.Codigo,
                Valor = acao.Valor,
                CodCorretora = _configuration["Corretora:Codigo"],
                NomeCorretora = _configuration["Corretora:Nome"]
            };
            var conteudoAcao = JsonSerializer.Serialize(cotacaoAcao);
            _logger.LogInformation($"Dados: {conteudoAcao}");

            string queueName = _configuration["RabbitMQ:Queue"];

            var factory = new ConnectionFactory()
            {
                Uri = new (_configuration["RabbitMQ:ConnectionString"])
            };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: queueName,
                                    durable: false,
                                    exclusive: false,
                                    autoDelete: false,
                                    arguments: null);
                
            channel.BasicPublish(exchange: "",
                                    routingKey: queueName,
                                    basicProperties: null,
                                    body: Encoding.UTF8.GetBytes(conteudoAcao));

            _logger.LogInformation(
                $"RabbitMQ - Envio para a fila {queueName} concluído | " +
                $"{conteudoAcao}");

            return new ()
            {
                Mensagem = "Informações de ação enviadas com sucesso!"
            };
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<AcaoDocument>), (int)HttpStatusCode.OK)]
        public List<AcaoDocument> ListAll()
        {
            return new MongoClient(_configuration["MongoDB:ConnectionString"])
                .GetDatabase(_configuration["MongoDB:Database"])
                .GetCollection<AcaoDocument>(_configuration["MongoDB:Collection"])
                .Find(all => true).ToList();
        }
    }
}