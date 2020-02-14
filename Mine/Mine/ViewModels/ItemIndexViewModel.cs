using Mine.Services;
using Mine.Models;
using Mine.Views;
using System.Diagnostics;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Xamarin.Forms;
using System.Linq;

namespace Mine.ViewModels
{
    /// <summary>
    /// Index View Model
    /// Manages the list of data records
    /// </summary>
    public class ItemIndexViewModel : BaseViewModel
    {
        private static volatile ItemIndexViewModel instance;
        private static readonly object syncRoot = new object();

        public static ItemIndexViewModel Instance
        {
            get
            {
                if (instance == null)
                {

                        lock (syncRoot)
                        {
                            if (instance == null)
                            {
                                instance = new ItemIndexViewModel();
                            }
                        }
                 }

                return instance;
            }
        }

        // The Data set of records
        public ObservableCollection<ItemModel> Dataset { get; set; }

        /// <summary>
        /// Connection to the Data store
        /// </summary>
        // public IDataStore<ItemModel> DataStore => DependencyService.Get<IDataStore<ItemModel>>();

        // Connections to the Data Stores
        public IDataStore<ItemModel> DataSource_Mock => new MockDataStore();
        public IDataStore<ItemModel> DataSource_SQL => new DatabaseService();
        public IDataStore<ItemModel> DataStore;
        public int CurrentDataSource = 0;


        // Command to force a Load of data
        public Command LoadDatasetCommand { get; set; }

        private bool _needsRefresh;

        /// <summary>
        /// Constructor
        /// 
        /// The constructor subscribes message listeners for crudi operations
        /// </summary>
        public ItemIndexViewModel()
        {
            Initialize();

            Title = "Items";

            Dataset = new ObservableCollection<ItemModel>();

            // Register the SetDataSource message.
            MessagingCenter.Subscribe<AboutPage, int>(this, "SetDataSource", async (obj, data) =>
            {
                await SetDataSource(data);
            });

            // Register the Create Message
            MessagingCenter.Subscribe<ItemCreatePage, ItemModel>(this, "Create", async (obj, data) =>
            {
                await Add(data as ItemModel);
            });

            // Register the Delete Message
            MessagingCenter.Subscribe<ItemDeletePage, ItemModel>(this, "Delete", async (obj, data) =>
            {
                await Delete(data as ItemModel);
            });

            // Register the Update Message
            MessagingCenter.Subscribe<ItemUpdatePage, ItemModel>(this, "Update", async (obj, data) =>
            {
                await Update(data as ItemModel);
            });

            // Register the Wipe Data List Message
            MessagingCenter.Subscribe<AboutPage, bool>(this, "WipeDataList", (obj, data) =>
            {
                WipeDataList();
            });

        }

        /// <summary>
        /// Wipe the data and set the refresh flag. 
        /// </summary>
        public void WipeDataList()
        {
            DataStore.WipeDataList();
            SetNeedsRefresh(true);
        }

        /// <summary>
        /// Initialize the ViewModel
        /// Sets the collection Dataset
        /// Sets the Load command
        /// Sets the default data source
        /// </summary>
        public async void Initialize()
        {
            Dataset = new ObservableCollection<ItemModel>();
            LoadDatasetCommand = new Command(async () => await ExecuteLoadDataCommand());

            await SetDataSource(CurrentDataSource);   // Set to Mock to start with
        }


        /// <summary>
        /// Select the appropriate Data Source
        /// </summary>
        /// <param name="isSQL"></param>
        /// <returns></returns>
        async public Task<bool> SetDataSource(int isSQL)
        {
            if (isSQL == 1)
            {
                DataStore = DataSource_SQL;
                CurrentDataSource = 1;
            }
            else
            {
                DataStore = DataSource_Mock;
                CurrentDataSource = 0;
            }

            await ExecuteLoadDataCommand();

            // Set flag for refresh
            SetNeedsRefresh(true);
            return await Task.FromResult(true);
        }


        /// <summary>
        /// API to add the Data
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<bool> Add(ItemModel data)
        {
            Dataset.Add(data);
            var result = await DataStore.CreateAsync(data);

            return true;
        }

        /// <summary>
        /// API to delete the Data
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<bool> Delete(ItemModel data)
        {
            // Check that the records exists. If it does not, then exit with false
            var record = await Read(data.Id);
            if (record == null)
            {
                return false;
            }

            Dataset.Remove(data);
            var result = await DataStore.DeleteAsync(data.Id);

            return result;
        }

        public async Task<bool> Update(ItemModel data)
        {
            // Check that the records exists. If it does not, then exit with false
            var record = await Read(data.Id);
            if (record == null)
            {
                return false;
            }

            record.Update(data);
            var result = await DataStore.UpdateAsync(record);

            await ExecuteLoadDataCommand();

            return result;
        }

        /// <summary>
        /// API to read the Data
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<ItemModel> Read(string id)
        {
            var result = await DataStore.ReadAsync(id);
            return result;
        }

        #region Refresh
        // Return True if a refresh is needed
        // It sets the refresh flag to false
        public bool NeedsRefresh()
        {
            if (_needsRefresh)
            {
                _needsRefresh = false;
                return true;
            }

            return false;
        }

        // Sets the need to refresh
        public void SetNeedsRefresh(bool value)
        {
            _needsRefresh = value;
        }

        // Command that Loads the Data
        private async Task ExecuteLoadDataCommand()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;

            try
            {
                Dataset.Clear();
                var dataset = await DataStore.IndexAsync(true);

                // Example of how to sort the database output using a linq query.
                // Sort the list
                dataset = dataset
                    .OrderBy(a => a.Name)
                    .ThenBy(a => a.Description)
                    .ToList();

                foreach (var data in dataset)
                {
                    Dataset.Add(data);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Force data to refresh
        /// </summary>
        public void ForceDataRefresh()
        {
            // Reset
            var canExecute = LoadDatasetCommand.CanExecute(null);
            LoadDatasetCommand.Execute(null);
        }
        #endregion Refresh
    }
}