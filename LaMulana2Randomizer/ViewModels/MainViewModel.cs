﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LaMulana2Randomizer.UI;
using LaMulana2Randomizer.Utils;
using Version = LaMulana2RandomizerShared.Version;

namespace LaMulana2Randomizer.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private string title;
        public string Title {
            get => title;
            set => Set(ref title, value);
        }

        private Settings settings;
        public Settings Settings 
        {
            get => settings;
            set => Set(ref settings, value);
        }

        public MainViewModel()
        {
            Title = "La Mulana 2 Randomiser " + Version.version;
            Settings = FileUtils.LoadSettings();
            Reroll();
        }

        private ICommand rerollCommand;
        public ICommand RerollCommand {
            get {
                if (rerollCommand == null)
                {
                    rerollCommand = new RelayCommand((x) => true, (x) => Reroll());
                }
                return rerollCommand;
            }
        }

        private ICommand generateCommand;
        public ICommand GenerateCommand {
            get {
                if (generateCommand == null)
                {
                    generateCommand = new RelayCommand((x) => true, (x) => Generate());
                }
                return generateCommand;
            }
        }

        public void Reroll()
        {
            Settings.Seed = new Random().Next(int.MinValue, int.MaxValue);
        }

        public void Generate()
        {
            FileUtils.SaveSettings(Settings);
            ProgressDialogViewModel dialogViewModel = new ProgressDialogViewModel();
            ProgressDialog dialog = new ProgressDialog()
            {
                DataContext = dialogViewModel,
                Owner = Application.Current.MainWindow
            };

            var task = Task.Factory.StartNew(() => GenerateSeed(dialogViewModel.progress));
            task.ContinueWith(_ => dialogViewModel.TaskComplete = true);
            dialog.ShowDialog();
        }

        public void GenerateSeed(IProgress<ProgressInfo> progress)
        {
            const int MaxEntranceAttempts = 1000;
            const int MaxItemAttempts = 250;

            Randomiser randomiser = new Randomiser(Settings);

            progress.Report(new ProgressInfo 
            { 
                Label = $"Generating Seed ", 
                ProgressValue = 0, 
                IsIndeterminate = true 
            });

            int attemptCount = 0;
            try
            {
                randomiser.Setup();
                bool entranceCheck;
                do
                {
                    attemptCount++;
                    randomiser.PlaceEntrances();
                    entranceCheck = randomiser.EntranceCheck();
                    if (!entranceCheck)
                    {
                        randomiser.ClearEntrances();
                        Logger.Log($"Failed to generate beatable entrance configuartion, retrying.");
                        progress.Report(new ProgressInfo
                        {
                            Label = $"Failed to generate beatable entrance configuartion, retrying attempt {attemptCount}.",
                            ProgressValue = 0,
                            IsIndeterminate = true
                        });
                    }

                } while (!entranceCheck && attemptCount < MaxEntranceAttempts);

                if (attemptCount == MaxEntranceAttempts && !entranceCheck)
                {
                    FileUtils.WriteSpoilerLog(randomiser);
                    Logger.LogAndFlush($"Failed to generate beatable entrance configuration for seed {randomiser.Settings.Seed}");
                    progress.Report(new ProgressInfo
                    {
                        Label = $"Failed to generate beatable entrance configuration, stopping after {MaxEntranceAttempts} attempts.",
                        ProgressValue = 100,
                        IsIndeterminate = false
                    });
                    return;
                }

                Logger.Log($"Generated beatable entrance configuartion after {attemptCount} attempts.");

                randomiser.FixAnkhLogic();
                randomiser.FixFDCLogic();

                attemptCount = 0;
                bool canBeatGame;
                do
                {
                    attemptCount++;
                    canBeatGame = false;

                    if(randomiser.PlaceItems())
                        canBeatGame = randomiser.CanBeatGame();

                    if (!canBeatGame)
                    {
                        randomiser.ClearRandomlyPlacedItems();
                        Logger.Log("Failed to generate beatable item placement, retrying.");
                        progress.Report(new ProgressInfo
                        {
                            Label = $"Failed to generate beatable item placement, retrying attempt {attemptCount}.",
                            ProgressValue = 0,
                            IsIndeterminate = true
                        });
                    }
                } while (!canBeatGame && attemptCount < MaxItemAttempts);

                if (attemptCount == MaxItemAttempts && !canBeatGame)
                {
                    FileUtils.WriteSpoilerLog(randomiser);
                    Logger.LogAndFlush($"Failed to generate beatable item placement for seed {randomiser.Settings.Seed}");
                    progress.Report(new ProgressInfo
                    {
                        Label = $"Failed to generate beatable item placement, stopping after {MaxItemAttempts} attempts.",
                        ProgressValue = 100,
                        IsIndeterminate = false
                    });
                    return;
                }
                randomiser.AdjustShopPrices();
                randomiser.FixEmptyLocations();
            }
            catch (RandomiserException ex)
            {
                Logger.Flush();
                progress.Report(new ProgressInfo
                {
                    Label = ex.Message,
                    ProgressValue = 100,
                    IsIndeterminate = false
                });
                return;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error generating seed: {randomiser.Settings.Seed}");
                Logger.LogAndFlush(ex.Message);
                progress.Report(new ProgressInfo
                {
                    Label = "Something has gone very wrong!",
                    ProgressValue = 100,
                    IsIndeterminate = false
                });
                return;
            }

            if (!FileUtils.WriteSpoilerLog(randomiser))
            {
                progress.Report(new ProgressInfo
                {
                    Label = "Failed to write spoiler log.",
                    ProgressValue = 100,
                    IsIndeterminate = false
                });
                return;
            }

            if (!FileUtils.WriteSeedFile(randomiser))
            {
                progress.Report(new ProgressInfo
                {
                    Label = "Failed to write seed file.",
                    ProgressValue = 100,
                    IsIndeterminate = false
                });
                return;
            }

            Logger.LogAndFlush($"Successfully generated for seed {randomiser.Settings.Seed} after {attemptCount} attempts.");
            progress.Report(new ProgressInfo
            {
                Label = "Successfully generated seed.",
                ProgressValue = 100,
                IsIndeterminate = false
            });
        }
    }
}
