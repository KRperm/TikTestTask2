using System;
using System.Windows;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace tikTestTask2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Хранилище тегов
        TagStorage storage;
        //Проверяет есть ли не сохраненный прогресс
        bool isUnsavedData;
        //фоновые потоки для загрузки и сохранения файла
        BackgroundWorker fileloadBackground, filesaveBackground;

        public MainWindow()
        {
            InitializeComponent();

            isUnsavedData = false;
            storage = new TagStorage();

            //Инициализируем 2 отдельных потока для загрузки и сохранения файла
            fileloadBackground = new BackgroundWorker();
            fileloadBackground.RunWorkerCompleted += fileloadBackgroundComplete;
            fileloadBackground.DoWork += fileloadBackgroundDoWork;
            fileloadBackground.WorkerReportsProgress = true;

            filesaveBackground = new BackgroundWorker();
            filesaveBackground.DoWork += filesaveBackgroundDoWork;
            filesaveBackground.RunWorkerCompleted += filesaveBackgroundComplete;

            treeView.ItemsSource = storage.Root.Childrens;
        }

        //Проверяет какой элемент древовидной структуры выделен и потом выполняет указанное действие
        delegate void TargetAction(TagItem target);
        private void CheckIfTargetExists(TargetAction DoIfExists)
        {
            TagItem target = treeView.SelectedItem as TagItem;

            if (target != null)
            {
                DoIfExists(target);
            }
            else
            {
                MessageBox.Show("Тег не выделен.", "Ошибка");
            }
        }

        //Показывает окно на удаление тега. При подтверждении пользователя - удаляет тег
        private void AskUserToDeleteTag(TagItem target)
        {
            if (new YesNoWindow(string.Format("Удалить тег '{0}' и всех его потомков?", target.Name), this).ShowDialog() == true)
            {
                target.Parent.RemoveChildByName(target.Name);
                isUnsavedData = true;
            }
        }
        //Показывает окно на добавление тега. Если все данные введены и есть поддтверждение, 
        //то новый элемент добавляется как дочерний элемент к целевому элементу
        private void AskUserToAddTag(TagItem target)
        {
            AddNewTagWindow confirmAdding = new AddNewTagWindow(this);

            if (confirmAdding.ShowDialog() == true)
            {
                if (confirmAdding.TextBoxValue.Length <= 0)
                {
                    MessageBox.Show("Задано пустое имя.", "Ошибка");
                }
                else if(!target.IsChildNameUnique(confirmAdding.TextBoxValue))
                {
                    MessageBox.Show("Такое имя уже есть среди детей родителя этого элемента.", "Ошибка");
                }
                else if (Regex.IsMatch(confirmAdding.TextBoxValue, "[^a-zA-Zа-яА-Я0-9]"))
                {
                    MessageBox.Show("Найдены недопустимые символы. Используйте только алфавиты кириллицы и латиницы, а так же цифры", "Ошибка");
                }
                else
                {
                    isUnsavedData = true;
                    switch (confirmAdding.SelectedType)
                    {
                        case "None":
                            target.AddChild(confirmAdding.TextBoxValue, null);
                            break;
                        case "Bool":
                            target.AddChild(confirmAdding.TextBoxValue, false);
                            break;
                        case "Int":
                            target.AddChild(confirmAdding.TextBoxValue, 0);
                            break;
                        case "Double":
                            target.AddChild(confirmAdding.TextBoxValue, 0.0);
                            break;
                    }
                }
               
            }
        }
        //Показывает окно для переименования. Переименовывает выбранный тег, если есть подтверждение и новое имя удовлетворяет условиям
        private void AskUserToRenameTag(TagItem target)
        {
            TextFieldWindow confirmRename = new TextFieldWindow(string.Format("Переименуй тег '{0}'", target.Name), this);
            if (confirmRename.ShowDialog() == true)
            {
                if (confirmRename.TextBoxValue.Length <= 0)
                {
                    MessageBox.Show("Задано пустое имя.", "Ошибка");
                }
                else if (Regex.IsMatch(confirmRename.TextBoxValue, "[^a-zA-Zа-яА-Я0-9]"))
                {
                    MessageBox.Show("Найдены недопустимые символы. Используйте только алфавиты кириллицы и латиницы, а так же цифры", "Ошибка");
                }
                else if (target.SetName(confirmRename.TextBoxValue))
                {
                    isUnsavedData = true;
                }else
                {
                    MessageBox.Show("Такое имя уже есть среди детей родителя этого элемента.", "Ошибка");
                }
            }
        }

        //Реализация событий
        private void OnClickAddTag(object sender, RoutedEventArgs e)
        {
            CheckIfTargetExists(AskUserToAddTag);
        }
        private void OnClickRenameTag(object sender, RoutedEventArgs e)
        {
            CheckIfTargetExists(AskUserToRenameTag);
        }

        private void OnClickDeleteTag(object sender, RoutedEventArgs e)
        {
            CheckIfTargetExists(AskUserToDeleteTag);
        }
        //Добавляет новый тег 1-ого уровня
        private void OnClickAddLevel1Tag(object sender, RoutedEventArgs e)
        {
            AskUserToAddTag(storage.Root);
        }

        //Пользователю выводится окно, где он выбирает какой файл загрузить. если файл существует - то данные загружаются и копируются в storage
        private void OnClickLoadFile(object sender, RoutedEventArgs e)
        {
            
            OpenFileDialog LoadFileWindow = new OpenFileDialog();
            LoadFileWindow.Filter = "Структура тегов в формате XML (*.XML)|*.xml" + "|Все файлы (*.*)|*.*";
            LoadFileWindow.CheckFileExists = true;
            LoadFileWindow.CheckPathExists = true;
            LoadFileWindow.Multiselect = false;
            if (isUnsavedData)
            {
                MessageBox.Show("Есть не сохраненные данные. При загрузке файла они будут утеряны", "Внимание");
            }
            if (LoadFileWindow.ShowDialog() == true)
            {
                SetUIControll(false);
                fileloadBackground.RunWorkerAsync(LoadFileWindow.FileName);
            }
        }

        //То что происходит в другом потоке
        private void fileloadBackgroundDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = LoadFile(e.Argument);
        }
        //после загрузки, все данные копируются в storage
        //Копирование происходит по той причине что список ObservableCollection созданый в одном потоке не может быть изменен в другом.  
        private void fileloadBackgroundComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result != null)
            {
                storage.Copy(e.Result as TagStorage);
            }
            SetUIControll(true);
        }

        //функция загрузки файла с выводом ошибки
        private TagStorage LoadFile(object filename)
        {
            TagStorage fromFile = new TagStorage();
            try
            {
                fromFile.LoadStructureFromFile(filename as string);
                isUnsavedData = false;
                return fromFile;
            }
            catch (Exception ex)
            {
                if (ex is FormatException)
                {
                    MessageBox.Show("Значение тега не соответствует указаному типу", "Ошибка");
                }
                else
                {
                    MessageBox.Show(ex.Message, "Ошибка!!");
                }
                return null;
            }
        }
        //Функция сохранения файла
        private void SaveFile(object filename)
        {
            isUnsavedData = false;
            storage.SaveStructureToFile(filename as string);
        }
        //Показывает окно, в котором пользователь выбирает папку и имя под которыми будет сохранен файл
        //Если все успешно то запускает процесс сохранения либо в фоном потоке, либо в потоке UI
        private void AskUserToSaveFile(bool isAsync)
        {
            SaveFileDialog SaveFileWindow = new SaveFileDialog();
            SaveFileWindow.Filter = "Структура тегов в формате XML (*.XML)|*.xml" + "|Все файлы (*.*)|*.*";
            SaveFileWindow.CheckPathExists = true;
            if (SaveFileWindow.ShowDialog() == true)
            {
                SetUIControll(false);
                if (isAsync)
                {
                    filesaveBackground.RunWorkerAsync(SaveFileWindow.FileName);
                }
                else
                {
                    SaveFile(SaveFileWindow.FileName);
                }
            }
        }

        private void filesaveBackgroundDoWork(object sender, DoWorkEventArgs e)
        {
            SaveFile(e.Argument);
        }

        private void filesaveBackgroundComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            SetUIControll(true);
        }

        private void OnClickSaveFile(object sender, RoutedEventArgs e)
        {
            AskUserToSaveFile(true);
        }


        private void OnCloseMainWindow(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isUnsavedData && new YesNoWindow("Есть не сохраненные данные. Сохранить их?", this).ShowDialog() == true)
            {
                //Сохранение происходит в потоке UI, так как программа завершается раньше, чем фоновый поток 
                //Поэтому в файл записываются не полные данные
                AskUserToSaveFile(false);
            }
        }

        //Включает/Выключает UI
        private void SetUIControll(bool canControll)
        {
            menu.IsEnabled = canControll;
            treeView.ContextMenu.IsEnabled = canControll;
        }
    }
}
