using System;
using System.IO;
using System.Text.RegularExpressions;

namespace tikTestTask2
{
    //Хранит и упраляет структурой Тегов
    class TagStorage
    {
        //файл сохраняется в той же папке что и приложение
        //const string file = "TagStructure.xml";
        //private string filepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);

        object locker = new object();
        private TagItem TagRoot = new TagItem("Root", null, null);

        public TagItem Root
        {
            get
            {
                return TagRoot;
            }
        }

        //Кодирует все дочерние теги в XML строку и записывает в файл. Делает это рекурсивно, поэтому это действие происходит со всеми вложенными тегами
        private void EncodeAndWriteTagsToFile(TagItem startTag, StreamWriter writer)
        {
            foreach (TagItem item in startTag.Childrens)
            {
                //Записываем Открывающий тег
                writer.Write("<tag name=\"{0}\" type=\"{1}\"", item.Name, item.Type);
                if (item.Type != "None")
                {
                    writer.Write(" value=\"{0}\">", item.Value);
                }else
                {
                    writer.Write(">");
                }
                //Записываем всех детей
                EncodeAndWriteTagsToFile(item, writer);
                //Записываем Закрывающий тег
                writer.Write("</tag>");
            }
        }

        //Запускает функцию EncodeAndWriteTagsToFile с корневого тега
        public void SaveStructureToFile(string filepath)
        {
            StreamWriter writer = new StreamWriter(filepath);
            writer.Write("<tag name=\"Root\" type=\"None\">");
            EncodeAndWriteTagsToFile(Root, writer);
            writer.Write("</tag>");
            writer.Close();
        }

        //Открывает XML файл и создает структуру из содержимого
        public bool LoadStructureFromFile(string filepath)
        {
            StreamReader reader;
            try
            {
                reader = new StreamReader(filepath);
            }
            catch
            {
                return false;
            }
            
            Regex tagRegEx = new Regex("(<tag[^>]*>|</tag>)");
            Regex nameRegEx = new Regex("(?<=name=\")\\w+(?=\")"); 
            Regex valueRegEx = new Regex("(?<=value=\")((\\d+(,\\d+)?)|True|False|None)(?=\")");
            Regex typeRegEx = new Regex("(?<=type=\")(None|Bool|Int|Double)(?=\")");
            //Добавление новых элементов происходит относительно этого указателся
            TagItem cursor = Root;
            //Находим все открывающие и закрывающие теги
            string rawData = reader.ReadToEnd();
            reader.Close();
            MatchCollection allTags = tagRegEx.Matches(rawData);
            //Проверяем валидность файла
            if (allTags.Count == 0)
            {
                throw new Exception("Неверный формат файла. Отсутствует структура");
            }
            if (allTags[0].Value != "<tag name=\"Root\" type=\"None\">")
            {
                throw new Exception("Неверный формат файла. Все содержимое должно находится в теге Root");
            }
            //Открывающий тег = +1; Закрывающий = -1; 
            //значение ушло в минус, то закрывающих слишком много
            //после окончания цикла оно не ноль - то открывающих много
            int openCloseTagsCheck = 1;
            foreach (Match hit in allTags)
            {
                if (openCloseTagsCheck <= 0)
                {
                    throw new Exception("Неверный формат файла. Некоторые теги не находятся внутри Элемента Root");
                }
                if (hit.Value == "</tag>")
                {
                    openCloseTagsCheck--;
                }
                else if(hit != allTags[0])
                {
                    openCloseTagsCheck++;
                }
            }
            if (openCloseTagsCheck > 0)
            {
                throw new Exception("Неверный формат файла. Открывающих тегов больше чем закрывающих");
            }

            TagRoot.Childrens.Clear();
            //Обрабатываем теги по порядку
            foreach (Match hit in allTags)
            {
                //Если тег закрывающий то это значит мы закончили добавлять детей в элемент структуры, на который ссылается указатель
                //указателем становится родитель этого элемета(движение вверх по дереву)
                if(hit.Value == "</tag>")
                {
                    cursor = cursor.Parent;
                }
                //Обрабатываем все теги кроме тега Root
                else if(hit.Value != "<tag name=\"Root\" type=\"None\">")
                {
                    //Верезаем значения из открывающего тега
                    string name = nameRegEx.Match(hit.Value).Value;
                    string value = valueRegEx.Match(hit.Value).Value;
                    string type = typeRegEx.Match(hit.Value).Value;

                    if(name.Length == 0)
                    {
                        throw new Exception("Имя тега не указано");
                    }

                    //В зависимости от типа добавляем новый элемент
                    //и указатель ссылается на этот элемент
                    switch (type)
                    {
                        case "None":
                            cursor = cursor.AddChild(name, null);
                            break;
                        case "Bool":
                            cursor = cursor.AddChild(name, bool.Parse(value));
                            break;
                        case "Int":
                            cursor = cursor.AddChild(name, int.Parse(value));
                            break;
                        case "Double":
                            cursor = cursor.AddChild(name, double.Parse(value));
                            break;
                        default:
                            throw new Exception("Неизвестный тип.");
                    }
                }
            }
            return true;
        }

        //Находит элемент по имени из всех вложенных дочерних элементов
        private TagItem FindChildOfItemByName(TagItem ItemToSearch, string name)
        {
            foreach (TagItem item in ItemToSearch.Childrens)
            {
                //Эемент найдей - возвращаем его
                if (item.Name == name)
                {
                    return item;
                }else
                {
                    //Иначе продолжаем поиски в дочернем элементе и возврщаем рузультат, если что-то нашли
                    TagItem SearchResult = FindChildOfItemByName(item, name);
                    if (SearchResult != null)
                    {
                        return SearchResult;
                    }
                }
            }
            //Если среди всех детей нет рузультата - то возвращаем null
            return null;
        }

        //Находим элемент по имени c тега Root
        public TagItem FindItemByName(string name)
        {
            return FindChildOfItemByName(Root, name);
        }

        //Возвращает элемент, который найден по этому пути
        //Делим путь на массив имен тегов и последовательно переходим от тега к тегу.
        public TagItem GetItemByFullPath(string path)
        {
            TagItem currentItem = Root;
            foreach (string itemName in path.Split('.'))
            {
                currentItem = currentItem.GetChildByName(itemName);
                if (currentItem == null)
                {
                    return null;
                }
            }
            return currentItem;
        }
        //Получить информацию о всех вложенных тегах
        private string GetAllChildTagsAsString(TagItem startItem)
        {
            string Result = "";
            foreach (TagItem item in startItem.Childrens)
            {
                //Записываем информацию о детях
                Result += GetAllChildTagsAsString(item);
            }
            //Возвращаем информацию о данном элементе и всех его дочерних элементов
            return string.Format("Тег: {0}; Уровень: {1}; Тип: {2}; Значение: {3}\n{4}", startItem.FullPath, startItem.Level, startItem.Type, startItem.Value, Result);
        }
        //Вывод информации о всей структуре
        public String GetAllTagsAsString()
        {
            return GetAllChildTagsAsString(Root);
        }

        public void Copy(TagStorage copyFrom)
        {
            Root.Childrens.Clear();
            CopyChildrens(Root, copyFrom.Root);
        }
        private void CopyChildrens(TagItem copyTo, TagItem copyFrom)
        {
            TagItem copyToChild;
            foreach(TagItem item in copyFrom.Childrens)
            {
                switch (item.Type)
                {
                    case "None":
                        copyToChild = copyTo.AddChild(item.Name, null);
                        break;
                    case "Bool":
                        copyToChild = copyTo.AddChild(item.Name, (bool)item.Value);
                        break;
                    case "Int":
                        copyToChild = copyTo.AddChild(item.Name, (int)item.Value);
                        break;
                    case "Double":
                        copyToChild = copyTo.AddChild(item.Name, (double)item.Value);
                        break;
                    default:
                        throw new Exception("Неизвестный тип.");
                }
                CopyChildrens(copyToChild, item);
            }
        }
    }
}
