﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices.Implementation.Utilities;
using static Microsoft.VisualStudio.LanguageServices.Implementation.ExtractInterface.ExtractInterfaceDialogViewModel;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp
{
    internal class PullMemberUpViewModel : AbstractNotifyPropertyChanged
    {
        public List<PullUpMemberSymbolView> SelectedMembersContainer { get; set; }

        public ObservableCollection<MemberSymbolViewModelGraphNode> TargetMembersContainer { get; set; }

        public Dictionary<ISymbol, Lazy<List<ISymbol>>> LazyDependentsMap { get; }
        
        public MemberSymbolViewModelGraphNode SelectedTarget { get; set; }
        
        internal PullMemberUpViewModel(
            List<ISymbol> allMembers,
            ObservableCollection<MemberSymbolViewModelGraphNode> targetMembersContainer,
            ISymbol userSelectNodeSymbol,
            IGlyphService glyphService,
            Dictionary<ISymbol, Lazy<List<ISymbol>>> lazyDependentsMap)
        {
            SelectedMembersContainer = allMembers.
                Select(member => new PullUpMemberSymbolView(member, glyphService)
                {
                    IsChecked = member.Equals(userSelectNodeSymbol),
                    IsAbstract = false,
                    IsAbstractSelectable = member.Kind != SymbolKind.Field && !member.IsAbstract,
                    IsSelectable = true
                }).ToList();
                
            TargetMembersContainer = targetMembersContainer;
            LazyDependentsMap = lazyDependentsMap;
        }
    }

    internal class PullUpMemberSymbolView : MemberSymbolViewModel
    {
        public bool IsAbstract { get; set; }

        private bool _isAbstractSelectable;
        
        public bool IsAbstractSelectable { get => _isAbstractSelectable; set => SetProperty(ref _isAbstractSelectable, value); }

        private bool _isSelectable;

        public bool IsSelectable { get => _isSelectable; set => SetProperty(ref _isSelectable, value); }

        public PullUpMemberSymbolView(ISymbol symbol, IGlyphService glyphService) : base(symbol, glyphService)
        {
        }
    }

    internal class MemberSymbolViewModelGraphNode : AbstractNotifyPropertyChanged
    {
        public MemberSymbolViewModel MemberSymbolViewModel { get; private set; }

        public ObservableCollection<MemberSymbolViewModelGraphNode> Neighbours { get; private set; }

        public bool IsExpanded { get; set; }

        public bool IsSelected { get => MemberSymbolViewModel.IsChecked; set => MemberSymbolViewModel.IsChecked = value; }

        private MemberSymbolViewModelGraphNode(MemberSymbolViewModel node, ObservableCollection<MemberSymbolViewModelGraphNode> descendants = null)
        {
            MemberSymbolViewModel = node;
            if (descendants == null)
            {
                Neighbours = new ObservableCollection<MemberSymbolViewModelGraphNode>();
            }
            else
            {
                Neighbours = descendants;
            }
        }

        /// <summary>
        /// Use breadth first search to create the inheritance graph. If several types share one same base type 
        /// then this base type will be put into the descendants of each types. This method assume there is no loop in the
        /// graph
        /// </summary>
        internal static MemberSymbolViewModelGraphNode CreateInheritanceGraph(INamedTypeSymbol root, IGlyphService glyphService)
        {
            var rootNode = new MemberSymbolViewModelGraphNode(new MemberSymbolViewModel(root, glyphService) { IsChecked = false});

            var queue = new Queue<MemberSymbolViewModelGraphNode>();
            queue.Enqueue(rootNode);

            while (queue.Any())
            {
                var currentNode = queue.Dequeue();
                var currentSymbol = currentNode.MemberSymbolViewModel.MemberSymbol as INamedTypeSymbol;

                var interfaces = currentSymbol.Interfaces.Where(@interface => @interface.DeclaringSyntaxReferences.Length > 0);
                var baseClass = currentSymbol.BaseType;
                if (baseClass != null && baseClass.DeclaringSyntaxReferences.Length == 0)
                {
                    baseClass = null;
                }
                AddBasesAndInterfaceToQueue(interfaces, baseClass, currentNode);
            }
            return rootNode;

            // A helper function to Add all baseTypes to queue and create a TreeNode for each of them
            void AddBasesAndInterfaceToQueue(
                IEnumerable<INamedTypeSymbol> interfaces,
                INamedTypeSymbol @class,
                MemberSymbolViewModelGraphNode parentNode)
            {
                if (interfaces != null)
                {
                    foreach (var @interface in interfaces)
                    {
                        var correspondingGraphNode = new MemberSymbolViewModelGraphNode(new MemberSymbolViewModel(@interface, glyphService))
                        {
                            IsExpanded = false, 
                        };
                        parentNode.Neighbours.Add(correspondingGraphNode);
                        queue.Enqueue(correspondingGraphNode);
                    }
                }

                if (@class != null)
                {
                    var correspondingGraphNode = new MemberSymbolViewModelGraphNode(new MemberSymbolViewModel(@class, glyphService))
                    {
                        IsExpanded = false, 
                    };
                    parentNode.Neighbours.Add(correspondingGraphNode);
                    queue.Enqueue(correspondingGraphNode);
                }
            }
        }
    }
}
