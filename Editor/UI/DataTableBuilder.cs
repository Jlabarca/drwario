#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrWario.Editor.UI
{
    /// <summary>
    /// Factory helper to create sortable MultiColumnListView instances for DrWario data tables.
    /// </summary>
    public static class DataTableBuilder
    {
        public struct ColumnDef
        {
            public string Title;
            public float Width;
            public bool Sortable;
        }

        /// <summary>
        /// Creates a MultiColumnListView with the given columns and data.
        /// </summary>
        /// <param name="columns">Column definitions</param>
        /// <param name="data">Data source list</param>
        /// <param name="bindCell">Callback to bind cell content: (element, rowIndex, columnIndex)</param>
        /// <param name="sortData">Callback to sort data: (columnIndex, ascending) → sorted list</param>
        /// <param name="height">Table height in pixels</param>
        public static VisualElement Create<T>(
            ColumnDef[] columns,
            List<T> data,
            Action<VisualElement, int, int> bindCell,
            Func<int, bool, List<T>> sortData = null,
            float height = 200)
        {
            var container = new VisualElement();

            if (data == null || data.Count == 0)
            {
                var emptyLabel = new Label("No data collected.")
                {
                    style =
                    {
                        color = new Color(0.5f, 0.5f, 0.5f),
                        fontSize = 11,
                        marginTop = 8,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                container.Add(emptyLabel);
                return container;
            }

            var tableColumns = new Columns();
            for (int i = 0; i < columns.Length; i++)
            {
                var col = new Column
                {
                    name = $"col{i}",
                    title = columns[i].Title,
                    width = columns[i].Width,
                    sortable = columns[i].Sortable
                };
                tableColumns.Add(col);
            }

            var listView = new MultiColumnListView(tableColumns)
            {
                fixedItemHeight = 20,
                itemsSource = data as System.Collections.IList,
                sortingEnabled = true
            };
            listView.style.height = height;
            listView.style.flexGrow = 1;

            // Bind cells
            for (int colIdx = 0; colIdx < columns.Length; colIdx++)
            {
                int capturedCol = colIdx;
                listView.columns[$"col{colIdx}"].makeCell = () =>
                {
                    var label = new Label { style = { fontSize = 11, unityTextAlign = TextAnchor.MiddleLeft } };
                    return label;
                };
                listView.columns[$"col{colIdx}"].bindCell = (element, row) =>
                {
                    bindCell(element, row, capturedCol);
                };
            }

            // Sort handling
            if (sortData != null)
            {
                listView.columnSortingChanged += () =>
                {
                    var sortDescs = listView.sortedColumns;
                    foreach (var sort in sortDescs)
                    {
                        // Find column index from name
                        string colName = sort.columnName;
                        if (colName.StartsWith("col") && int.TryParse(colName.Substring(3), out int colIndex))
                        {
                            bool ascending = sort.direction == SortDirection.Ascending;
                            var sorted = sortData(colIndex, ascending);
                            data.Clear();
                            data.AddRange(sorted);
                            listView.RefreshItems();
                        }
                        break; // Only use first sort column
                    }
                };
            }

            container.Add(listView);
            return container;
        }
    }
}
#endif
