// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.Formatting
{
    internal readonly struct TriviaList(SyntaxTriviaList list1, SyntaxTriviaList list2)
    {
        public int Count => list1.Count + list2.Count;

        public SyntaxTrivia this[int index]
            => index < list1.Count ? list1[index] : list2[index - list1.Count];

        public SyntaxTriviaList List1 => list1;
        public SyntaxTriviaList List2 => list2;

        public Enumerator GetEnumerator()
            => new(this);

        public struct Enumerator
        {
            private readonly SyntaxTriviaList _list1;
            private readonly SyntaxTriviaList _list2;

            private SyntaxTriviaList.Enumerator _enumerator;
            private int _index;

            internal Enumerator(TriviaList triviaList)
            {
                _list1 = triviaList.List1;
                _list2 = triviaList.List2;

                _index = -1;
                _enumerator = _list1.GetEnumerator();
            }

            public bool MoveNext()
            {
                _index++;
                if (_index == _list1.Count)
                {
                    _enumerator = _list2.GetEnumerator();
                }

                return _enumerator.MoveNext();
            }

            public SyntaxTrivia Current => _enumerator.Current;
        }
    }
}
