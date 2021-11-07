using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TransactionsMicroservice.Models;

namespace TransactionsMicroservice.Services
{
    public class TransactionService
    {
        private readonly TransactionContext _context;

        // private string accountApi = "http://localhost:41001/api/";
        private string accountApi = "https://bankaccountapi.azurewebsites.net/api/";

        // private string rulesApi = "http://localhost:41003/api/";
        private string rulesApi = "https://bankrulesapi.azurewebsites.net/api/";
        public TransactionService(TransactionContext context)
        {
            _context = context;
        }

        public List<Transaction> GetTransactions(int id)
        {
            List<Transaction> transactions;
            try
            {
                transactions = _context.Transactions.Where(t => t.AccountId == id).ToList();
                return transactions;
            }
            catch (DbUpdateConcurrencyException Dbce)
            {
                Console.WriteLine(Dbce.Message);
            }
            catch (DbUpdateException Dbe)
            {
                Console.WriteLine(Dbe.Message);
            }
            return null;
        }

        public async Task<string> DepositAsync(int accountId, float amount)
        {
            string status = "";
            Transaction transaction = new Transaction();
            AccountDTO account = null;
            
            try
            {
                var getTask = new HttpResponseMessage();
                var client = new HttpClient();
                client.BaseAddress = new Uri(accountApi);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");
                getTask = await client.GetAsync("Account/getAccount/" + accountId);

                var rules = new HttpClient();
                rules.BaseAddress = new Uri(rulesApi);
                var getCharges = rules.GetAsync($"GetCharges?balance={amount}&accountId={accountId}");
                getCharges.Wait();
                var resRules = getCharges.Result;
                if (getTask.IsSuccessStatusCode & resRules.IsSuccessStatusCode)
                {
                    var data = getTask.Content.ReadFromJsonAsync<AccountDTO>();
                    string chargesString = resRules.Content.ReadAsStringAsync().Result;
                    float charges;
                    float.TryParse(chargesString, NumberStyles.Float, CultureInfo.InvariantCulture, out charges);
                    if (data.Status.ToString() != "RanToCompletion")
                    {
                        status = "There is no account with this Id";
                        return status;
                    }
                    else
                    {
                        account = data.Result;
                        account.Balance += amount;
                        account.Balance -= charges;
                        int accountBalance = (int)account.Balance;
                        var putTask = await client.PutAsJsonAsync<AccountDTO>("account/" + accountId, account);

                        if (putTask.IsSuccessStatusCode)
                        {
                            transaction.Amount = amount;
                            transaction.AccountId = accountId;
                            transaction.Date = DateTime.Now;
                            transaction.TargerAccountId = accountId;
                            transaction.Type = "Deposit";
                            transaction.AccountBalance = accountBalance;
                            status = "Success";
                            transaction.TransactionStatus = status;
                            _context.Add(transaction);
                            _context.SaveChanges();
                        }
                    }
                }
            }
            catch (DbUpdateConcurrencyException Dbce)
            {
                status = Dbce.Message;
                Console.WriteLine(Dbce.Message);
            }
            catch (DbUpdateException Dbe)
            {
                status = Dbe.Message;
                Console.WriteLine(Dbe.Message);
            }
            return status;
        }

        public string Withdraw(int accountId, float amount)
        {
            string status = "";
            Transaction transaction = new Transaction();
            AccountDTO account = null;
            var client = new HttpClient();
            client.BaseAddress = new Uri(accountApi);
            var rules = new HttpClient();
            rules.BaseAddress = new Uri(rulesApi);
            try
            {
                var getTask = client.GetAsync("account/getAccount/" + accountId);
                getTask.Wait();
                var result = getTask.Result;

                var getRules = rules.GetAsync($"GetRules?balance={amount}&accId={accountId}");
                getRules.Wait();
                var resRules = getRules.Result.Content.ReadAsStringAsync().Result;
                if (resRules!= "Allowed")
                {
                    status = resRules;
                    return status;
                }
                if (result.IsSuccessStatusCode)
                {
                    var data = result.Content.ReadFromJsonAsync<AccountDTO>();
                    if (data.Status.ToString() != "RanToCompletion")
                    {
                        status = "There is no account with this Id";
                        return status;
                    }
                    else
                    {
                        account = data.Result;
                        account.Balance -= amount;
                        float accountBalance = account.Balance;
                        var putTask = client.PutAsJsonAsync<AccountDTO>("account/" + accountId, account);
                        putTask.Wait();
                        var result1 = putTask.Result;
                        if (result1.IsSuccessStatusCode)
                        {
                            transaction.Amount = amount;
                            transaction.AccountId = accountId;
                            transaction.Date = DateTime.Now;
                            transaction.AccountBalance = accountBalance;
                            transaction.TargerAccountId = accountId;
                            transaction.Type = "Withdraw";
                            status = "Success";
                            transaction.TransactionStatus = status;
                            _context.Add(transaction);
                            _context.SaveChanges();
                        }
                    }
                }
            }
            catch (DbUpdateConcurrencyException Dbce)
            {
                status = Dbce.Message;
                Console.WriteLine(Dbce.Message);
            }
            catch (DbUpdateException Dbe)
            {
                status = Dbe.Message;
                Console.WriteLine(Dbe.Message);
            }
            return status;
        }

        public string Transfer(int sourceAccountId, int targerAccountId, float amount)
        {
            string status = "";
            Transaction transaction = new Transaction();
            AccountDTO sourceAccount = null;
            AccountDTO targerAccount = null;
            var client = new HttpClient();
            client.BaseAddress = new Uri(accountApi);
            var rules = new HttpClient();
            rules.BaseAddress = new Uri(rulesApi);

            try
            {
                var getTaskFrom = client.GetAsync("Account/getAccount/" + sourceAccountId);
                getTaskFrom.Wait();
                var resultFrom = getTaskFrom.Result;
                var getTaskTo = client.GetAsync("Account/getAccount/" + targerAccountId);
                getTaskTo.Wait();
                var resultTo = getTaskTo.Result;
                
                var getRules = rules.GetAsync($"GetRules?balance={amount}&accId={sourceAccountId}");
                getRules.Wait();
                var resRules = getRules.Result.Content.ReadAsStringAsync().Result;
                if (resRules != "Allowed")
                {
                    status = resRules;
                    return status;
                }
                if (resultFrom.IsSuccessStatusCode&&resultTo.IsSuccessStatusCode)
                {
                    var dataFrom = resultFrom.Content.ReadFromJsonAsync<AccountDTO>();
                    var dataTo = resultTo.Content.ReadFromJsonAsync<AccountDTO>();
                    if (dataFrom.Status.ToString() != "RanToCompletion" || dataTo.Status.ToString() != "RanToCompletion")
                    {
                        status = "There is no account with this Id";
                        return status;
                    }
                    else
                    {
                        sourceAccount = dataFrom.Result;
                        targerAccount = dataTo.Result;
                       
                        sourceAccount.Balance -= amount;
                        float accountBalance = sourceAccount.Balance;

                        var putTaskFrom = client.PutAsJsonAsync<AccountDTO>("Account/" + sourceAccountId, sourceAccount);
                        putTaskFrom.Wait();
                        var result1 = putTaskFrom.Result;

                        targerAccount.Balance += amount;
                        var putTaskTo = client.PutAsJsonAsync<AccountDTO>("Account/" + targerAccountId, targerAccount);
                        putTaskTo.Wait();
                        var result2 = putTaskFrom.Result;
                        if (result2.IsSuccessStatusCode)
                        {
                            transaction.Amount = amount;
                            transaction.AccountId = sourceAccountId;
                            transaction.Date = DateTime.Now;
                            transaction.SourceAccountId = sourceAccountId;
                            transaction.AccountBalance = accountBalance;
                            transaction.TargerAccountId = targerAccountId;
                            transaction.Type = "Transfer";
                            status = "Success";
                            transaction.TransactionStatus = status;
                            _context.Add(transaction);
                            _context.SaveChanges();
                        }
                    }
                }

            }
            catch (DbUpdateConcurrencyException Dbce)
            {
                status = Dbce.Message;
                Console.WriteLine(Dbce.Message);
            }
            catch (DbUpdateException Dbe)
            {
                status = Dbe.Message;
                Console.WriteLine(Dbe.Message);
            }
            return status;
        }
    }
}
