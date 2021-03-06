﻿using DataAccess.Contract.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataAccess
{
    public class CopyFilesRepository : IDataAccessRepository
    {
        private List<FileInfo> _filesFound = new List<FileInfo>();
        private DirectoryInfo _sourceDirectory;
        private DirectoryInfo _destinationDirectory;

        public void SetSourceDirectory(string path)
        {
            try
            {
                if (path == null)
                {
                    _sourceDirectory = null;
                    return;
                }
                _sourceDirectory = new DirectoryInfo(path);
            }
            catch
            {
                throw new DirectoryNotFoundException("Папка с исходными изображениями не найдена");
            }
        }

        public void SetDestinationDirectory(string path)
        {
            try
            {
                if (path == null)
                {
                    _destinationDirectory = null;
                    return;
                }
                var destPath = path;
                if (!Directory.Exists(destPath))
                {
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    destPath = Path.Combine(desktopPath, path);
                    Directory.CreateDirectory(destPath);
                }
                _destinationDirectory = new DirectoryInfo(destPath);
            }
            catch
            {
                throw new DirectoryNotFoundException("Целевая папка не создана или не доступна");
            }
        }

        public string CopyFile(string filename)
        {
            try
            {
                if (!Directory.Exists(_sourceDirectory.FullName))
                {
                    throw new DirectoryNotFoundException("Папка с исходными изображениями не доступна");
                }

                if (!Directory.Exists(_destinationDirectory.FullName))
                {
                    throw new DirectoryNotFoundException("Целевая папка не доступна");
                }

                var item = _filesFound.Find(f => f.FullName == filename);
                var newFilePath = Path.Combine(_destinationDirectory.FullName, Path.GetFileName(filename));
                if (File.Exists(newFilePath))
                {
                    throw new ArgumentException($"Файл {item.Name} уже существует");
                }

                File.Copy(item.FullName, newFilePath);

                if (File.Exists(newFilePath))
                {
                    return newFilePath;
                }

                throw new FileNotFoundException($"Файл {filename} не скопирован");
            }
            catch (ArgumentException fae)
            {
                throw new ArgumentException(fae.Message);
            }
            catch (FileNotFoundException fnf)
            {
                throw new FileNotFoundException(fnf.Message);
            }
            catch (DirectoryNotFoundException dnf)
            {
                throw new DirectoryNotFoundException(dnf.Message);
            }
        }

        public int FindAllFiles()
        {
            _filesFound.Clear();
            try
            {
                var files = FindAccessableFiles(_sourceDirectory.FullName, "*.jpg", true)
                        .Select(file => new FileInfo(file)).ToList();
                _filesFound.AddRange(files);
            }
            catch
            {
                throw new IOException("Возникла ошибка при попытке получить список файлов из исходной папки");
            }
            return _filesFound.Count;
        }

        private IEnumerable<string> FindAccessableFiles(string path, string file_pattern, bool recurse)
        {
            Console.WriteLine(path);
            var required_extension = "jpg";

            if (File.Exists(path))
            {
                yield return path;
                yield break;
            }

            if (!Directory.Exists(path))
            {
                yield break;
            }

            if (null == file_pattern)
                file_pattern = "*." + required_extension;

            var top_directory = new DirectoryInfo(path);

            IEnumerator<FileInfo> files;
            try
            {
                files = top_directory.EnumerateFiles(file_pattern).GetEnumerator();
            }
            catch (Exception)
            {
                files = null;
            }

            while (true)
            {
                FileInfo file = null;
                try
                {
                    if (files?.MoveNext() == true)
                        file = files.Current;
                    else
                        break;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (PathTooLongException)
                {
                    continue;
                }

                yield return file.FullName;
            }

            if (!recurse)
                yield break;

            IEnumerator<DirectoryInfo> dirs;
            try
            {
                dirs = top_directory.EnumerateDirectories("*").GetEnumerator();
            }
            catch (Exception)
            {
                dirs = null;
            }


            while (true)
            {
                DirectoryInfo dir = null;
                try
                {
                    if (dirs?.MoveNext() == true)
                        dir = dirs.Current;
                    else
                        break;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (PathTooLongException)
                {
                    continue;
                }

                foreach (var subpath in FindAccessableFiles(dir.FullName, file_pattern, recurse))
                    yield return subpath;
            }
        }

        public IReadOnlyCollection<FileInfo> FindFile(string filename, bool startsWith)
        {
            try
            {
                if (_sourceDirectory == null || !_sourceDirectory.Exists)
                {
                    return null;
                }
                if (_filesFound.Count == 0)
                {
                    FindAllFiles();
                }
                return startsWith
                    ? _filesFound.Where(f => f.Name.StartsWith(filename, StringComparison.OrdinalIgnoreCase)).ToArray()
                    : _filesFound.Where(f => f.Name.IndexOf(filename, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            }
            catch
            {
                throw new FileNotFoundException("Возникла ошибка при поиске файла");
            }
        }

        public bool IsSourceDirectorySet() => _sourceDirectory != null;

        public bool IsDestinationDirectorySet() => _destinationDirectory != null;

        public void DeleteFile(string id)
        {
            try
            {
                var item = _filesFound.Find(f => f.Name == id + ".jpg");
                var fileForDelete = Path.Combine(_destinationDirectory.FullName, Path.GetFileName(item.FullName));
                File.Delete(fileForDelete);
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message);
            }
        }

        public string LoadFromDestinationDirectory()
        {
            try
            {
                if (IsDestinationDirectorySet())
                {
                    var jsonFiles = _destinationDirectory.GetFiles("*.json", SearchOption.TopDirectoryOnly);
                    if (jsonFiles.Length > 0)
                    {
                        return File.ReadAllText(jsonFiles.FirstOrDefault()?.FullName);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new FileLoadException(ex.Message);
            }
        }

        public IReadOnlyCollection<FileInfo> EnumerateDestinationDirectoryFiles()
        {
            try
            {
                return _destinationDirectory.GetFiles("*.jpg", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message);
            }
        }

        public void SaveProcessedImagesList(string filesList)
        {
            try
            {
                if (IsDestinationDirectorySet())
                {
                    string jsonFilePath = null;
                    try
                    {
                        jsonFilePath = Directory.GetFiles(_destinationDirectory.FullName, "*.json", SearchOption.TopDirectoryOnly).First();
                    }
                    catch
                    {
                        jsonFilePath = Path.Combine(_destinationDirectory.FullName, $"{_destinationDirectory.Name}.json");
                    }

                    SaveContent(jsonFilePath, filesList);
                }
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message);
            }
        }

        private static void SaveContent(string jsonFilePath, string filesList)
        {
            if (File.Exists(jsonFilePath))
            {
                File.SetAttributes(jsonFilePath, FileAttributes.Normal);
            }
            File.WriteAllText(jsonFilePath, filesList);
            File.SetAttributes(jsonFilePath, FileAttributes.Hidden);
        }

        public string GetDestinationDirectoryPath()
        {
            return IsDestinationDirectorySet() ? _destinationDirectory.FullName : string.Empty;
        }
    }
}
