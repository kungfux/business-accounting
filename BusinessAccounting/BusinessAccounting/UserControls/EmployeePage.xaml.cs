﻿using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XDatabase;

namespace BusinessAccounting.UserControls
{
    /// <summary>
    /// Interaction logic for EmployeePage.xaml
    /// </summary>
    public partial class EmployeePage
    {
        public EmployeePage()
        {
            InitializeComponent();
        }

        public static RoutedCommand NewEmployeeCommand = new RoutedCommand();
        public static RoutedCommand OpenEmployeeCommand = new RoutedCommand();
        public static RoutedCommand EditEmployeeCommand = new RoutedCommand();
        public static RoutedCommand SaveEmployeeCommand = new RoutedCommand();
        public static RoutedCommand DeleteEmployeeCommand = new RoutedCommand();
        public static RoutedCommand FindEmployeeCommand = new RoutedCommand();
        public static RoutedCommand FindAllEmployeesCommand = new RoutedCommand();
        public static RoutedCommand LoadAllHistoryCommand = new RoutedCommand();
        public static RoutedCommand LookupPhotoCommand = new RoutedCommand();
        public static RoutedCommand RemovePhotoCommand = new RoutedCommand();

        private Employee _openedEmployee;
        private List<CashTransaction> _salaryHistory;
        private List<Employee> _foundEmployees;

        private const int PreloadRecordsCount = 10;

        #region Functionality methods
        private void SearchEmployees(bool pShowAll = false)
        {
            _foundEmployees = new List<Employee>();

            DataTable employees = null;
            string query = "select Id, fullname from ba_employees_cardindex";
            if (pShowAll)
            {
                query += ";";
            }
            else
            {
                switch (((ComboBoxItem)ComboSearchCriteria.SelectedItem).Name)
                {
                    case "FullName":
                        query += " where fullname like @data;";
                        break;
                    case "Phone":
                        query += " where telephone like @data;";
                        break;
                }
            }

            if (pShowAll)
            {
                employees = App.Sqlite.SelectTable(query);
            }
            else
            {
                employees = App.Sqlite.SelectTable(query,
                    new XParameter("@data", "%" + InputSearchData.Text + "%"));
            }

            if (employees != null && employees.Rows.Count > 0)
            {
                foreach (DataRow r in employees.Rows)
                {
                    _foundEmployees.Add(new Employee()
                    {
                        Id = Convert.ToInt32(r.ItemArray[0]),
                        FullName = r.ItemArray[1].ToString()
                    });
                }
            }
            else
            {
                ShowMessage("Никто не найден!");
            }

            LbEmployees.ItemsSource = _foundEmployees;
        }

        private void OpenEmployee()
        {
            if (LbEmployees.SelectedIndex != -1)
            {
                DataRow r = App.Sqlite.SelectRow("select Id, fullname, hired, fired, document, telephone, address, notes  from ba_employees_cardindex where Id=@Id;",
                    new XParameter("@Id", _foundEmployees[LbEmployees.SelectedIndex].Id));
                if (r == null)
                {
                    ShowMessage("Сотрудник не найден.");
                    return;
                }
                _openedEmployee = new Employee()
                {
                    Id = Convert.ToInt32(r.ItemArray[0]),
                    FullName = r.ItemArray[1].ToString(),
                    Hired = r.ItemArray[2] != DBNull.Value ? (DateTime?)r.ItemArray[2] : null,
                    Fired = r.ItemArray[3] != DBNull.Value ? (DateTime?)r.ItemArray[3] : null,
                    Document = r.ItemArray[4].ToString(),
                    Telephone = r.ItemArray[5].ToString(),
                    Address = r.ItemArray[6].ToString(),
                    Notes = r.ItemArray[7].ToString()
                };

                LoadPhoto();

                DataContext = _openedEmployee;
                LoadSalaryHistory();
                ClearInputFields(false);
            }
        }

        private void LoadPhoto()
        {
            // retrieve photo
            System.Drawing.Image image = App.Sqlite.SelectBinaryAsImage("select photo from ba_employees_cardindex where Id=@Id;",
                new XParameter("@Id", _openedEmployee.Id));

            if (image != null)
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    image.Save(memory, ImageFormat.Png);
                    memory.Position = 0;
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    _openedEmployee.Photo = bitmapImage;
                }
            }
            else
            {
                _openedEmployee.Photo = new BitmapImage(new Uri("pack://application:,,,/Resources/image_noimage.png"));
            }
        }

        private void LoadSalaryHistory(bool all = false)
        {
            string query =
                string.Format("select Id, datestamp, summa, Comment from ba_cash_operations where Id in (select opid from ba_employees_cash where emid = @emid) order by Id desc;",
                // TODO: add {0}
                all ? "" : "limit " + PreloadRecordsCount);

            _salaryHistory = new List<CashTransaction>();

            DataTable historyRecords = App.Sqlite.SelectTable(query,
                new XParameter("@emid", _openedEmployee.Id));
            if (historyRecords != null)
            {
                foreach (DataRow row in historyRecords.Rows)
                {
                    _salaryHistory.Add(new CashTransaction()
                    {
                        Id = Convert.ToInt32(row.ItemArray[0].ToString()),
                        Date = Convert.ToDateTime(row.ItemArray[1]),
                        Sum = 0 - decimal.Parse(row.ItemArray[2].ToString()),
                        Comment = row.ItemArray[3].ToString()
                    });
                }
                LvSalaryHistory.ItemsSource = _salaryHistory;
            }
        }

        private bool SaveEmployee()
        {
            if (_openedEmployee.Id != 0)
            {
                // change record
                return App.Sqlite.Update(
                    "update ba_employees_cardindex set hired = @h, fired = @f, fullname = @name, document = @d, telephone = @t, address = @a, notes = @n where Id = @Id;",
                    new XParameter("@h", _openedEmployee.Hired),
                    new XParameter("@f", _openedEmployee.Fired),
                    new XParameter("@name", _openedEmployee.FullName),
                    new XParameter("@d", _openedEmployee.Document),
                    new XParameter("@t", _openedEmployee.Telephone),
                    new XParameter("@a", _openedEmployee.Address),
                    new XParameter("@n", _openedEmployee.Notes),
                    new XParameter("@Id", _openedEmployee.Id)) > 0;
            }
            else
            {
                // save new
                return App.Sqlite.Insert(
                    "insert into ba_employees_cardindex (hired, fired, fullname, document, telephone, address, notes) values (@h, @f, @name, @d, @t, @a, @n);",
                    new XParameter("@h", _openedEmployee.Hired),
                    new XParameter("@f", _openedEmployee.Fired),
                    new XParameter("@name", _openedEmployee.FullName),
                    new XParameter("@d", _openedEmployee.Document),
                    new XParameter("@t", _openedEmployee.Telephone),
                    new XParameter("@a", _openedEmployee.Address),
                    new XParameter("@n", _openedEmployee.Notes)) > 0;
            }
        }

        private bool DeleteEmployee()
        {
            return App.Sqlite.Delete("delete from ba_employees_cardindex where Id=@Id",
                new XParameter("@Id", _openedEmployee.Id)) > 0;
        }

        private void ClearInputFields(bool isEnabled, bool clearValues = false)
        {
            PickerHiredDate.IsEnabled = isEnabled;
            PickerFiredDate.IsEnabled = isEnabled;
            InputEmplName.IsReadOnly = !isEnabled;
            InputEmplPhone.IsReadOnly = !isEnabled;
            InputEmplPassport.IsReadOnly = !isEnabled;
            InputEmplAddress.IsReadOnly = !isEnabled;
            InputEmplNotes.IsReadOnly = !isEnabled;

            if (clearValues)
            {
                PickerHiredDate.SelectedDate = null;
                PickerFiredDate.SelectedDate = null;
                InputEmplName.Text = "";
                InputEmplPhone.Text = "";
                InputEmplPassport.Text = "";
                InputEmplAddress.Text = "";
                InputEmplNotes.Text = "";
            }
        }

        private void ChoosePhoto()
        {
            System.Windows.Forms.OpenFileDialog ofDialog = new System.Windows.Forms.OpenFileDialog
            {
                AddExtension = true,
                Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png"
            };
            if (ofDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (ofDialog.FileName != "")
                {
                    try
                    {
                        System.Drawing.Image image = System.Drawing.Image.FromFile(ofDialog.FileName); // check that file is image
                        if (image == null)
                        {
                            ShowMessage("Выбранный файл не является изображением и не может быть использован в качестве фотографии!");
                        }
                    }
                    catch (Exception) 
                    {
                        ShowMessage("Выбранный файл не является изображением и не может быть использован в качестве фотографии!");
                    }

                    if (!App.Sqlite.InsertFileIntoCell(ofDialog.FileName, "update ba_employees_cardindex set photo = @file where Id = @Id", "@file",
                        new XParameter("@Id", _openedEmployee.Id)))
                    {
                        ShowMessage("Не удалось сохранить фотографию сотрудника!");
                    }
                    else
                    {
                        LoadPhoto();
                    }
                }
            }
        }

        private void ClearPhoto()
        {
            if (App.Sqlite.Update("update ba_employees_cardindex set photo = null where Id=@Id;",
                    new XParameter("@Id", _openedEmployee.Id)) > 0)
            {
                LoadPhoto();
            }
            else
            {
                ShowMessage("Не удалось удалить фотографию сотрудника!");
            }
        }

        private async void bRemoveHistoryRecord_Click(object sender, RoutedEventArgs e)
        {
            CashTransaction record = null;

            for (var visual = sender as Visual; visual != null; visual = VisualTreeHelper.GetParent(visual) as Visual)
                if (visual is GridViewRowPresenter)
                {
                    var row = (GridViewRowPresenter)visual;
                    record = (CashTransaction)row.DataContext;
                    break;
                }

            await AskAndDelete(string.Format("Удалить запись?{0}{0}Информация об удаляемой записи:{0} Дата: {1:dd MMMM yyyy}{0} Сумма: {2:C}{0} Комментарий: {3}",
                Environment.NewLine, record.Date, record.Sum, record.Comment), record);
        }

        private async Task AskAndDelete(string question, CashTransaction record)
        {
            MetroWindow w = (MetroWindow)Parent.GetParentObject().GetParentObject();
            MessageDialogResult result = await w.ShowMessageAsync("Вопросик", question, MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                if (App.Sqlite.Delete("delete from ba_cash_operations where Id = @Id;",
                        new XParameter("@Id", record.Id)) <= 0)
                {
                    ShowMessage("Не удалось удалить запись из базы данных!");
                }
                else
                {
                    LoadSalaryHistory();
                }
            }
        }

        private void ShowMessage(string text)
        {
            for (var visual = this as Visual; visual != null; visual = VisualTreeHelper.GetParent(visual) as Visual)
                if (visual is MetroWindow)
                {
                    ((MetroWindow)visual).ShowMessageAsync("Проблемка", text + Environment.NewLine + App.Sqlite.LastErrorMessage,
                        MessageDialogStyle.Affirmative);
                }
        }
        #endregion

        #region Commands
        private void Find_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                ComboSearchCriteria.SelectedIndex != -1 && // search criteria is selected
                InputSearchData.Text != ""; // search key is defined
        }

        private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SearchEmployees();
        }

        private void FindAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void FindAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SearchEmployees(true);
        }

        private void Open_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = LbEmployees.SelectedIndex != -1; // employee is selected
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenEmployee();
        }

        private void New_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void New_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _openedEmployee = new Employee();
            DataContext = _openedEmployee;
            ClearInputFields(true);
        }

        private void Edit_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                _openedEmployee != null && 
                _openedEmployee.Id != 0 &&
                !PickerHiredDate.IsEnabled;
        }

        private void Edit_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ClearInputFields(true);
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                _openedEmployee != null &&
                PickerHiredDate.IsEnabled &&
                PickerHiredDate.SelectedDate != null &&
                (PickerFiredDate.SelectedDate != null ? PickerHiredDate.SelectedDate <= PickerFiredDate.SelectedDate : true) &&
                InputEmplName.Text != "";
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (SaveEmployee())
            {
                ClearInputFields(false, true);
                _openedEmployee = null;
                DataContext = _openedEmployee;
            }
        }

        private void Delete_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                _openedEmployee != null && _openedEmployee.Id != 0;
        }

        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DeleteEmployee())
            {
                ClearInputFields(false, true);
                _openedEmployee = null;
                DataContext = _openedEmployee;
            }
        }

        private void LoadAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = 
                _openedEmployee != null &&
                _openedEmployee.Id != 0 &&
                LvSalaryHistory.Items.Count <= PreloadRecordsCount;
        }

        private void LoadAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            LoadSalaryHistory(true);
        }

        private void LookupPhoto_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ChoosePhoto();
        }

        private void LookupPhoto_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                _openedEmployee != null && _openedEmployee.Id != 0;
        }

        private void RemovePhoto_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ClearPhoto();
        }

        private void RemovePhoto_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                _openedEmployee != null && _openedEmployee.Id != 0 &&
                _openedEmployee.Photo != null;
        }
        #endregion

        private void listFoundEmpl_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenEmployee();
        }
    }
}