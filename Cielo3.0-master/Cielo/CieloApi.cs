﻿using Cielo.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cielo
{
    public class CieloApi
    {
        private readonly static HttpClient _http = new HttpClient();

        /// <summary>
        /// Tempo para TimeOut da requisição, por default é 60 segundos
        /// </summary>
        private readonly int _timeOut = 0;

        public Merchant Merchant { get; }
        public ISerializerJSON SerializerJSON { get; }
        public CieloEnvironment Environment { get; }

        static CieloApi()
        {
            _http.DefaultRequestHeaders.ExpectContinue = false;

            /*
             O proxy pode ser definido no Web.config ou App.config da sua aplicação
             */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="merchant"></param>
        /// <param name="serializer">Crie o seu Provider Json</param>
        /// <param name="timeOut">Tempo para TimeOut da requisição, por default é 60 segundos</param>
        public CieloApi(CieloEnvironment environment, Merchant merchant, ISerializerJSON serializer, int timeOut = 60000, SecurityProtocolType securityProtocolType = SecurityProtocolType.Tls12)
        {
            Environment = environment;
            Merchant = merchant;
            SerializerJSON = serializer;
            _timeOut = timeOut;

            ServicePointManager.SecurityProtocol = securityProtocolType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="merchant"></param>
        /// <param name="serializer">Crie o seu Provider Json</param>
        /// <param name="timeOut">Tempo para TimeOut da requisição, por default é 60 segundos</param>
        public CieloApi(Merchant merchant, ISerializerJSON serializer, int timeOut = 60000)
                : this(CieloEnvironment.PRODUCTION, merchant, serializer, timeOut) { }

        private IDictionary<string, string> GetHeaders(Guid requestId)
        {
            return new Dictionary<string, string>(1)
            {
                { "RequestId", requestId.ToString() }
            };
        }

        private async Task<HttpResponseMessage> CreateRequestAsync(string resource, Method method = Method.GET, IDictionary<string, string> headers = null)
        {
            return await CreateRequestAsync<object>(resource, null, method, headers);
        }

        private async Task<HttpResponseMessage> CreateRequestAsync<T>(string resource, T value, Method method = Method.POST, IDictionary<string, string> headers = null)
        {
            StringContent content = null;

            if (value != null)
            {
                string json = SerializerJSON.Serialize<T>(value);
                content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await ExecuteAsync(resource, headers, method, content);
        }

        private async Task<HttpResponseMessage> ExecuteAsync(string fullUrl, IDictionary<string, string> headers = null, Method method = Method.POST, StringContent content = null)
        {
            if (headers == null)
            {
                headers = new Dictionary<string, string>(2);
            }

            headers.Add("MerchantId", Merchant.Id.ToString());
            headers.Add("MerchantKey", Merchant.Key);

            var tokenSource = new CancellationTokenSource(_timeOut);

            try
            {
                HttpMethod httpMethod = null;

                if (method == Method.POST)
                {
                    httpMethod = HttpMethod.Post;
                    if (headers != null)
                    {
                        foreach (var item in headers)
                        {
                            content.Headers.Add(item.Key, item.Value);
                        }
                    }
                }
                else if (method == Method.GET)
                {
                    httpMethod = HttpMethod.Get;
                }
                else if (method == Method.PUT)
                {
                    httpMethod = HttpMethod.Put;
                }
                else if (method == Method.DELETE)
                {
                    httpMethod = HttpMethod.Delete;
                }

                using (var request = GetExecute(fullUrl, headers, httpMethod, content))
                {
                    return await _http.SendAsync(request, tokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new CancellationTokenException(e);
            }
            finally
            {
                tokenSource.Dispose();
            }
        }

        private static HttpRequestMessage GetExecute(string fullUrl, IEnumerable<KeyValuePair<string, string>> headers, HttpMethod method, StringContent content = null)
        {
            var request = new HttpRequestMessage(method, fullUrl)
            {
                Content = content
            };

            if (method != HttpMethod.Post)
            {
                foreach (var item in headers)
                {
                    request.Headers.Add(item.Key, item.Value);
                }
            }

            return request;
        }
        private async Task<T> GetResponseAsync<T>(HttpResponseMessage response)
        {
            await CheckResponseAsync(response).ConfigureAwait(false);

            return await SerializerJSON.DeserializeAsync<T>(response.Content).ConfigureAwait(false);
        }

        private async Task CheckResponseAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new CieloException($"Error code {response.StatusCode}.", result, this.SerializerJSON);
            }
        }

        /// <summary>
        /// Envia uma transação
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public async Task<Transaction> CreateTransactionAsync(Guid requestId, Transaction transaction)
        {
            if (transaction != null &&
                transaction.Customer != null &&
                transaction.Customer.Address != null)
            {
                var error = transaction.Customer.Address.IsValid();
                if (!string.IsNullOrEmpty(error))
                {
                    throw new CieloException(error, "1");
                }
            }

            var headers = GetHeaders(requestId);
            using (var response = await CreateRequestAsync(Environment.GetTransactionUrl("/1/sales/"), transaction, Method.POST, headers))
            {

                return await GetResponseAsync<Transaction>(response);
            }
        }

        /// <summary>
        /// Consulta uma transação
        /// </summary>
        /// <param name="paymentId"></param>
        /// <returns></returns>
        public async Task<Transaction> GetTransactionAsync(Guid paymentId)
        {
            return await GetTransactionAsync(Guid.NewGuid(), paymentId);
        }

        /// <summary>
        /// Consulta uma transação
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="paymentId"></param>
        /// <returns></returns>
        public async Task<Transaction> GetTransactionAsync(Guid requestId, Guid paymentId)
        {
            var headers = GetHeaders(requestId);

            using (var response = await CreateRequestAsync(Environment.GetQueryUrl($"/1/sales/{paymentId}"), Method.GET, headers))
            {
                return await GetResponseAsync<Transaction>(response);
            }
        }


        /// <summary>
        /// Consulta uma transação
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="paymentId"></param>
        /// <returns></returns>
        public async Task<ReturnRecurrentPayment> GetRecurrentPaymentAsync(Guid requestId, Guid recurrentPaymentId)
        {
            var headers = GetHeaders(requestId);
            var response = await CreateRequestAsync(Environment.GetQueryUrl($"/1/RecurrentPayment/{recurrentPaymentId}"), Method.GET, headers);

            return await GetResponseAsync<ReturnRecurrentPayment>(response);
        }

        /// <summary>
        /// Cancela uma transação (parcial ou total)
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="paymentId"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public async Task<ReturnStatus> CancelTransactionAsync(Guid requestId, Guid paymentId, decimal? amount = null)
        {
            var url = Environment.GetTransactionUrl($"/1/sales/{paymentId}/void");

            if (amount.HasValue)
            {
                url += $"?Amount={NumberHelper.DecimalToInteger(amount)}";
            }

            var headers = GetHeaders(requestId);
            var response = await CreateRequestAsync(url, Method.PUT, headers);

            return await GetResponseAsync<ReturnStatus>(response);
        }

        /// <summary>
        /// Captura uma transação (parcial ou total)
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="paymentId"></param>
        /// <param name="amount"></param>
        /// <param name="serviceTaxAmount"></param>
        /// <returns></returns>
        public async Task<ReturnStatus> CaptureTransactionAsync(Guid requestId, Guid paymentId, decimal? amount = null, decimal? serviceTaxAmount = null)
        {
            var url = Environment.GetTransactionUrl($"/1/sales/{paymentId}/capture");

            if (amount.HasValue)
            {
                url += $"?Amount={NumberHelper.DecimalToInteger(amount)}";
            }

            if (serviceTaxAmount.HasValue)
            {
                url += $"?SeviceTaxAmount={NumberHelper.DecimalToInteger(serviceTaxAmount)}";
            }

            var headers = GetHeaders(requestId);
            var response = await CreateRequestAsync(url, Method.PUT, headers);

            return await GetResponseAsync<ReturnStatus>(response);
        }

        /// <summary>
        /// Ativa uma recorrência
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="recurrentPaymentId"></param>
        /// <exception cref="CieloException">Ocorreu algum erro ao tentar alterar a recorrência</exception>
        /// <returns></returns>
        public async Task<bool> ActivateRecurrentAsync(Guid requestId, Guid recurrentPaymentId)
        {
            return await ManagerRecurrent(requestId, recurrentPaymentId, true);
        }

        /// <summary>
        /// Desativa uma recorrência
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="recurrentPaymentId"></param>
        /// <exception cref="CieloException">Ocorreu algum erro ao tentar alterar a recorrência</exception>
        /// <returns></returns>
        public async Task<bool> DeactivateRecurrentAsync(Guid requestId, Guid recurrentPaymentId)
        {
            return await ManagerRecurrent(requestId, recurrentPaymentId, false);
        }

        /// <summary>
        /// Cria uma Token de um cartão válido ou não.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="recurrentPaymentId"></param>
        /// <param name="active">Parâmetro que define se uma recorrência será desativada ou ativada novamente</param>
        /// <exception cref="CieloException">Ocorreu algum erro ao tentar alterar a recorrência</exception>
        /// <returns>Se retornou true é porque a operação foi realizada com sucesso</returns>
        public async Task<ReturnStatusLink> CreateTokenAsync(Guid requestId, Card card)
        {
            card.CustomerName = card.Holder;
            card.SecurityCode = string.Empty;

            var url = Environment.GetTransactionUrl($"/1/Card");
            var headers = GetHeaders(requestId);
            var response = await CreateRequestAsync(url, card, Method.POST, headers);

            //Se tiver errado será levantado uma exceção
            return await GetResponseAsync<ReturnStatusLink>(response);
        }

        /// <summary>
        /// Faz pagamento de 1 real e cancela logo em seguida para testar se o cartão é válido.
        /// Gera token somente de cartão válido
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="card"></param>
        /// <returns></returns>
        public async Task<ReturnStatusLink> CreateTokenValidAsync(Guid requestId, Card creditCard, string softDescriptor = "Validando", Currency currency = Currency.BRL)
        {
            creditCard.SaveCard = true;
            creditCard.CustomerName = creditCard.Holder;

            var customer = new Customer(creditCard.CustomerName);

            var payment = new Payment(amount: 1,
                                      currency: currency,
                                      paymentType: PaymentType.CreditCard,
                                      installments: 1,
                                      capture: true,
                                      recurrentPayment: null,
                                      softDescriptor: softDescriptor,
                                      card: creditCard,
                                      returnUrl: string.Empty);

            var transaction = new Transaction(Guid.NewGuid().ToString(), customer, payment);

            var result = await CreateTransactionAsync(requestId, transaction);
            var status = result.Payment.GetStatus();
            if (status == Status.Authorized || status == Status.PaymentConfirmed)
            {
                //Cancelando pagamento de 1 REAL
                var resultCancel = await CancelTransactionAsync(Guid.NewGuid(), result.Payment.PaymentId.Value, 1);
                var status2 = resultCancel.GetStatus();
                if (status2 != Status.Voided)
                {
                    return new ReturnStatusLink
                    {
                        ReturnCode = resultCancel.ReturnCode,
                        ReturnMessage = resultCancel.ReturnMessage,
                        Status = resultCancel.Status,
                        Links = resultCancel.Links.FirstOrDefault()
                    };
                }
            }
            else
            {
                return new ReturnStatusLink
                {
                    ReturnCode = result.Payment.ReturnCode,
                    ReturnMessage = result.Payment.ReturnMessage,
                    Status = result.Payment.Status,
                    Links = result.Payment.Links.FirstOrDefault()
                };
            }

            var token = result.Payment.CreditCard.CardToken.HasValue ? result.Payment.CreditCard.CardToken.Value.ToString() : string.Empty;
            var statusLink = new ReturnStatusLink
            {
                CardToken = token,
                ReturnCode = result.Payment.ReturnCode,
                ReturnMessage = result.Payment.ReturnMessage,
                Status = result.Payment.Status,
                Links = result.Payment.Links.FirstOrDefault()
            };

            return statusLink;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="recurrentPaymentId"></param>
        /// <param name="active">Parâmetro que define se uma recorrência será desativada ou ativada novamente</param>
        /// <exception cref="CieloException">Ocorreu algum erro ao tentar alterar a recorrência</exception>
        /// <returns>Se retornou true é porque a operação foi realizada com sucesso</returns>
        private async Task<bool> ManagerRecurrent(Guid requestId, Guid recurrentPaymentId, bool active)
        {
            var url = Environment.GetTransactionUrl($"/1/RecurrentPayment/{recurrentPaymentId}/Deactivate");

            if (active)
            {
                //Ativar uma recorrência novamente
                url = Environment.GetTransactionUrl($"/1/RecurrentPayment/{recurrentPaymentId}/Reactivate");
            }

            var headers = GetHeaders(requestId);
            var response = await CreateRequestAsync(url, Method.PUT, headers);

            //Se tiver errado será levantado uma exceção
            await CheckResponseAsync(response);

            return true;
        }

        #region Método Sincronos

        /// <summary>
        ///  Cria uma Token de um cartão válido ou não.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="card"></param>
        /// <returns></returns>
        public ReturnStatusLink CreateToken(Guid requestId, Card card)
        {
            return RunTask(() =>
            {
                return CreateTokenAsync(requestId, card);
            });
        }

        public ReturnRecurrentPayment GetRecurrentPayment(Guid requestId, Guid recurrentPaymentId)
        {
            return RunTask(() =>
            {
                return GetRecurrentPaymentAsync(requestId, recurrentPaymentId);
            });
        }

        public ReturnMerchandOrderID GetMerchandOrderID(string merchantOrderId)
        {
            return RunTask(() =>
            {
                return GetMerchandOrderIDAsync(merchantOrderId);
            });
        }

        /// <summary>
        /// Consulta uma transação
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="paymentId"></param>
        /// <returns></returns>
        public async Task<ReturnMerchandOrderID> GetMerchandOrderIDAsync(string merchantOrderId)
        {
            var headers = GetHeaders(Guid.NewGuid());
            using (var response = await CreateRequestAsync(Environment.GetQueryUrl($"/1/sales?merchantOrderId={merchantOrderId}"), Method.GET, headers))
            {

                return await GetResponseAsync<ReturnMerchandOrderID>(response);
            }
        }

        /// <summary>
        /// Faz pagamento de 1 real e cancela logo em seguida para testar se o cartão é válido.
        /// Gera token somente de cartão válido
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="creditCard"></param>
        /// <param name="softDescriptor"></param>
        /// <param name="currency"></param>
        /// <returns></returns>
        public ReturnStatusLink CreateTokenValid(Guid requestId, Card creditCard, string softDescriptor = "Validando", Currency currency = Currency.BRL)
        {
            return RunTask(() =>
            {
                return CreateTokenValidAsync(requestId, creditCard, softDescriptor, currency);
            });
        }

        /// <summary>
        /// Captura uma transação (parcial ou total)
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="paymentId"></param>
        /// <param name="amount"></param>
        /// <param name="serviceTaxAmount"></param>
        /// <returns></returns>
        public ReturnStatus CaptureTransaction(Guid requestId, Guid paymentId, decimal? amount = null, decimal? serviceTaxAmount = null)
        {
            return RunTask(() =>
            {
                return CaptureTransactionAsync(requestId, paymentId, amount, serviceTaxAmount);
            });
        }

        public bool ActivateRecurrent(Guid requestId, Guid recurrentPaymentId)
        {
            return RunTask(() =>
            {
                return ActivateRecurrentAsync(requestId, recurrentPaymentId);
            });
        }

        public bool DeactivateRecurrent(Guid requestId, Guid recurrentPaymentId)
        {
            return RunTask(() =>
            {
                return DeactivateRecurrentAsync(requestId, recurrentPaymentId);
            });
        }

        public ReturnStatus CancelTransaction(Guid requestId, Guid paymentId, decimal? amount = null)
        {
            return RunTask(() =>
            {
                return CancelTransactionAsync(requestId, paymentId, amount);
            });
        }

        /// <summary>
        /// Consulta uma transação
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="paymentId"></param>
        /// <returns></returns>
        public Transaction GetTransaction(Guid requestId, Guid paymentId)
        {
            return RunTask(() =>
            {
                return GetTransactionAsync(requestId, paymentId);
            });
        }

        /// <summary>
        /// Consulta uma transação
        /// </summary>
        /// <param name="paymentId"></param>
        /// <returns></returns>
        public Transaction GetTransaction(Guid paymentId)
        {
            return RunTask(() =>
            {
                return GetTransactionAsync(paymentId);
            });
        }

        /// <summary>
        /// Envia uma transação
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public Transaction CreateTransaction(Guid requestId, Transaction transaction)
        {
            return RunTask(() =>
            {
                return CreateTransactionAsync(requestId, transaction);
            });
        }

        private static TResult RunTask<TResult>(Func<Task<TResult>> method)
        {
            try
            {
                return Task.Run(method).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                if (e.InnerException is CieloException ex)
                {
                    throw ex;
                }
                else if (e is CieloException exCielo)
                {
                    throw exCielo;
                }

                throw;
            }
        }
        #endregion
    }
}
