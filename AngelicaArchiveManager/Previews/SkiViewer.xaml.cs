using AngelicaArchiveManager.Core.ArchiveEngine;
using AngelicaArchiveManager.Interfaces;
using AngelicaArchiveManager.Previews.Models;
using System.Windows;
using System.Threading.Tasks;

namespace AngelicaArchiveManager.Previews
{
    public partial class SkiViewer : Window, IPreviewWin
    {
        public IArchiveManager Manager { get; set; }
        public string Path { get; set; }
        public IArchiveEntry File { get; set; }

        public SkiViewer()
        {
            InitializeComponent();
        }

        public async void Prepare()
        {
            byte[] fileData = await Manager.GetFileAsync(File);
            SkiReader Ski = new SkiReader(fileData)
            {
                Manager = Manager,
                ModelFilePath = Path
            };
            Model.Content = await Ski.GetModelAsync();
        }
    }
}
