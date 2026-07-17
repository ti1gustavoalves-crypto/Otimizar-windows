using System;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace CodexPerformanceOptimizer
{
    internal static class StorageDeletion
    {
        public static string GetBlockReason(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return "Selecione um arquivo ou uma pasta válida.";
                string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return "O item selecionado não existe mais.";

                string root = Path.GetPathRoot(fullPath);
                if (string.IsNullOrWhiteSpace(root) || root.Length < 3 || root[1] != ':') return "Somente itens de discos locais podem ser enviados para a Lixeira.";
                if (string.Equals(fullPath, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return "A raiz de um disco não pode ser excluída.";

                var drive = new DriveInfo(root);
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) return "Somente itens de discos locais fixos podem ser enviados para a Lixeira.";
                if (IsCriticalRootPath(fullPath, root)) return "Este local é necessário para a inicialização, recuperação ou proteção do Windows.";

                FileAttributes attributes = File.GetAttributes(fullPath);
                if ((attributes & FileAttributes.System) != 0) return "Este é um item protegido pelo sistema e não pode ser excluído pelo Otimizador.";

                string[] protectedTrees =
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                };
                foreach (string protectedTree in protectedTrees)
                    if (IsSameOrChild(fullPath, protectedTree)) return "Este local é protegido para evitar danos ao Windows ou aos programas instalados.";

                string executable = Path.GetFullPath(typeof(StorageDeletion).Assembly.Location);
                if (string.Equals(fullPath, executable, StringComparison.OrdinalIgnoreCase)) return "O executável em uso não pode ser excluído.";

                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar);
                string profilesRoot = Path.GetDirectoryName(profile);
                if (string.Equals(fullPath, profile, StringComparison.OrdinalIgnoreCase) || string.Equals(fullPath, profilesRoot, StringComparison.OrdinalIgnoreCase))
                    return "A pasta principal de usuários não pode ser excluída pelo Otimizador.";

                return string.Empty;
            }
            catch (Exception ex)
            {
                return "Não foi possível validar o item: " + ex.Message;
            }
        }

        public static string MoveToRecycleBin(string path)
        {
            string blocked = GetBlockReason(path);
            if (!string.IsNullOrWhiteSpace(blocked)) return blocked;
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                    FileSystem.DeleteDirectory(fullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                else
                    FileSystem.DeleteFile(fullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                return "Movido para a Lixeira: " + fullPath;
            }
            catch (OperationCanceledException)
            {
                return "A exclusão foi cancelada pelo Windows.";
            }
            catch (Exception ex)
            {
                return "Não foi possível mover o item para a Lixeira: " + ex.Message;
            }
        }

        private static bool IsSameOrChild(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsCriticalRootPath(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRoot = Path.GetFullPath(root);
            string[] criticalNames = { "Recovery", "Boot", "EFI", "System Volume Information", "$Recycle.Bin", "Documents and Settings" };
            foreach (string name in criticalNames)
                if (IsSameOrChild(normalizedPath, Path.Combine(normalizedRoot, name))) return true;
            return false;
        }
    }
}
