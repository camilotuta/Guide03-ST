
// MainForm.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guia03.Core.DTOs;
using Guia03nterfaces;
using Guia03odels;

namespace Guia03ms
{
    public partial class MainForm : Form
    {
        private readonly ITransactionService _transactionService;
        private readonly IAccountService _accountService;
        private readonly BankingGrpcClient _grpcClient;

        public MainForm(ITransactionService transactionService, IAccountService accountService)
        {
            InitializeComponent();
            _transactionService = transactionService;
            _accountService = accountService;
            _grpcClient = new BankingGrpcClient("https://localhost:7001"); // gRPC endpoint

            LoadAccountsAsync();
        }

        private async void LoadAccountsAsync()
        {
            try
            {
                var accounts = await _accountService.GetAllAccountsAsync();

                cmbFromAccount.Items.Clear();
                cmbToAccount.Items.Clear();
                cmbBalanceAccount.Items.Clear();

                foreach (var account in accounts)
                {
                    var item = $"{account.AccountNumber} ({account.AccountType})";
                    cmbFromAccount.Items.Add(new ComboBoxItem { Text = item, Value = account.AccountId });
                    cmbToAccount.Items.Add(new ComboBoxItem { Text = item, Value = account.AccountId });
                    cmbBalanceAccount.Items.Add(new ComboBoxItem { Text = item, Value = account.AccountId });
                }

                // Actualizar DataGridView con cuentas
                dgvAccounts.DataSource = accounts.Select(a => new
                {
                    AccountId = a.AccountId,
                    AccountNumber = a.AccountNumber,
                    AccountType = a.AccountType,
                    Balance = a.Balance.ToString("C"),
                    CreatedDate = a.CreatedDate.ToString("yyyy-MM-dd")
                }).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading accounts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnCreateAccount_Click(object sender, EventArgs e)
        {
            try
            {
                string accountType = cmbAccountType.SelectedItem?.ToString() ?? "Savings";
                var newAccount = await _accountService.CreateAccountAsync(accountType);

                MessageBox.Show($"Account created successfully!\nAccount Number: {newAccount.AccountNumber}",
                               "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadAccountsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating account: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnTransfer_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateTransferInputs()) return;

                var fromAccountId = ((ComboBoxItem)cmbFromAccount.SelectedItem).Value;
                var toAccountId = ((ComboBoxItem)cmbToAccount.SelectedItem).Value;
                var amount = decimal.Parse(txtAmount.Text);

                // Usar gRPC para la transferencia
                var result = await _grpcClient.ProcessTransferAsync(fromAccountId, toAccountId, amount, txtDescription.Text);

                if (result.Success)
                {
                    MessageBox.Show($"Transfer completed successfully!\nTransaction ID: {result.TransactionId}\nNew Balance: {result.NewBalance:C}",
                                   "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    ClearTransferInputs();
                    await LoadAccountsAsync();
                }
                else
                {
                    MessageBox.Show($"Transfer failed: {result.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing transfer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnDeposit_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateDepositInputs()) return;

                var accountId = ((ComboBoxItem)cmbToAccount.SelectedItem).Value;
                var amount = decimal.Parse(txtDepositAmount.Text);

                var request = new TransactionRequest
                {
                    ToAccountId = accountId,
                    Amount = amount,
                    TransactionType = "Deposit",
                    Description = "Deposit via Windows Forms"
                };

                var result = await _transactionService.ProcessDepositAsync(request);

                if (result.Success)
                {
                    MessageBox.Show($"Deposit completed successfully!\nTransaction ID: {result.TransactionId}\nNew Balance: {result.NewBalance:C}",
                                   "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    txtDepositAmount.Clear();
                    await LoadAccountsAsync();
                }
                else
                {
                    MessageBox.Show($"Deposit failed: {result.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing deposit: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnWithdraw_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateWithdrawalInputs()) return;

                var accountId = ((ComboBoxItem)cmbFromAccount.SelectedItem).Value;
                var amount = decimal.Parse(txtWithdrawAmount.Text);

                var request = new TransactionRequest
                {
                    FromAccountId = accountId,
                    Amount = amount,
                    TransactionType = "Withdrawal",
                    Description = "Withdrawal via Windows Forms"
                };

                var result = await _transactionService.ProcessWithdrawalAsync(request);

                if (result.Success)
                {
                    MessageBox.Show($"Withdrawal completed successfully!\nTransaction ID: {result.TransactionId}\nNew Balance: {result.NewBalance:C}",
                                   "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    txtWithdrawAmount.Clear();
                    await LoadAccountsAsync();
                }
                else
                {
                    MessageBox.Show($"Withdrawal failed: {result.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing withdrawal: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnCheckBalance_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbBalanceAccount.SelectedItem == null)
                {
                    MessageBox.Show("Please select an account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var accountId = ((ComboBoxItem)cmbBalanceAccount.SelectedItem).Value;
                var balance = await _transactionService.GetAccountBalanceAsync(accountId);

                if (balance != null)
                {
                    lblBalance.Text = $"Balance: {balance.Balance:C}";
                    MessageBox.Show($"Account: {balance.AccountNumber}\nType: {balance.AccountType}\nBalance: {balance.Balance:C}",
                                   "Account Balance", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Account not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking balance: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnTransactionHistory_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbBalanceAccount.SelectedItem == null)
                {
                    MessageBox.Show("Please select an account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var accountId = ((ComboBoxItem)cmbBalanceAccount.SelectedItem).Value;
                var transactions = await _transactionService.GetAccountTransactionsAsync(accountId);

                // Mostrar en DataGridView
                dgvTransactions.DataSource = transactions.Select(t => new
                {
                    TransactionId = t.TransactionId,
                    Type = t.TransactionType,
                    Amount = t.Amount.ToString("C"),
                    Date = t.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    Description = t.Description,
                    Status = t.Status
                }).ToList();

                tabControl.SelectedTab = tabTransactions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading transaction history: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Prueba de concurrencia - Procesar múltiples transacciones simultáneamente
        private async void btnStressTest_Click(object sender, EventArgs e)
        {
            try
            {
                var accounts = await _accountService.GetAllAccountsAsync();
                if (accounts.Count < 2)
                {
                    MessageBox.Show("Need at least 2 accounts for stress test.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var random = new Random();
                var requests = new List<TransactionRequest>();

                // Crear 100 transacciones aleatorias
                for (int i = 0; i < 100; i++)
                {
                    var fromAccount = accounts[random.Next(accounts.Count)];
                    var toAccount = accounts[random.Next(accounts.Count)];

                    if (fromAccount.AccountId != toAccount.AccountId)
                    {
                        requests.Add(new TransactionRequest
                        {
                            FromAccountId = fromAccount.AccountId,
                            ToAccountId = toAccount.AccountId,
                            Amount = random.Next(1, 100),
                            TransactionType = "Transfer",
                            Description = $"Stress test transaction {i + 1}"
                        });
                    }
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                lblStatus.Text = "Processing concurrent transactions...";
                lblStatus.Refresh();

                var result = await _transactionService.ProcessMultipleTransactionsAsync(requests);

                stopwatch.Stop();

                lblStatus.Text = $"Completed in {stopwatch.ElapsedMilliseconds}ms";

                MessageBox.Show($"Stress test completed!\nTransactions processed: {requests.Count}\nSuccess rate: {(result ? "100%" : "<100%")}\nTime: {stopwatch.ElapsedMilliseconds}ms",
                               "Stress Test Results", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadAccountsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during stress test: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Validation Methods

        private bool ValidateTransferInputs()
        {
            if (cmbFromAccount.SelectedItem == null)
            {
                MessageBox.Show("Please select source account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (cmbToAccount.SelectedItem == null)
            {
                MessageBox.Show("Please select destination account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount greater than zero.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var fromAccountId = ((ComboBoxItem)cmbFromAccount.SelectedItem).Value;
            var toAccountId = ((ComboBoxItem)cmbToAccount.SelectedItem).Value;

            if (fromAccountId == toAccountId)
            {
                MessageBox.Show("Source and destination accounts must be different.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private bool ValidateDepositInputs()
        {
            if (cmbToAccount.SelectedItem == null)
            {
                MessageBox.Show("Please select an account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!decimal.TryParse(txtDepositAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount greater than zero.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private bool ValidateWithdrawalInputs()
        {
            if (cmbFromAccount.SelectedItem == null)
            {
                MessageBox.Show("Please select an account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!decimal.TryParse(txtWithdrawAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount greater than zero.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void ClearTransferInputs()
        {
            txtAmount.Clear();
            txtDescription.Clear();
            cmbFromAccount.SelectedIndex = -1;
            cmbToAccount.SelectedIndex = -1;
        }

        #endregion
    }

    public class ComboBoxItem
    {
        public string Text { get; set; }
        public int Value { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}