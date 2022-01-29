﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels;

public class HocrDocumentViewModel : ViewModelBase, IUndoRedoCommandsService
{
    public UndoRedoManager UndoRedoManager { get; } = new();

    public ObservableCollection<HocrPageViewModel> Pages { get; }

    public ICollectionView PagesCollectionView { get; }

    public HocrPageViewModel? CurrentPage
    {
        get => (HocrPageViewModel?)PagesCollectionView.CurrentItem;
        set => PagesCollectionView.MoveCurrentTo(value);
    }

    public bool ShowText { get; set; }

    public IRelayCommand<HocrPageViewModel> DeletePageCommand { get; }

    public IRelayCommand NextPageCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }

    public HocrDocumentViewModel(IEnumerable<HocrPageViewModel> pages)
    {
        Pages = new ObservableCollection<HocrPageViewModel>(pages);

        PagesCollectionView = CollectionViewSource.GetDefaultView(Pages);
        PagesCollectionView.CurrentChanged += PagesCollectionViewOnCurrentChanged;

        DeletePageCommand = new RelayCommand<HocrPageViewModel>(DeletePage, CanDeletePage);

        NextPageCommand = new RelayCommand(() => PagesCollectionView.MoveCurrentToNext(), () => !PagesCollectionView.IsCurrentLast());
        PreviousPageCommand = new RelayCommand(() => PagesCollectionView.MoveCurrentToPrevious(), () => !PagesCollectionView.IsCurrentFirst());
    }

    public bool CanDeletePage(HocrPageViewModel? page) => page != null && Pages.Contains(page);

    public void DeletePage(HocrPageViewModel? page)
    {
        if (page == null)
        {
            return;
        }

        UndoRedoManager.ExecuteCommand(Pages.ToCollectionRemoveCommand(page));
    }

    public HocrDocumentViewModel() : this(Enumerable.Empty<HocrPageViewModel>())
    {
    }

    private void PagesCollectionViewOnCurrentChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentPage));
    }
}
