using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using XRL.World;

using SerializeField = UnityEngine.SerializeField;

namespace UD_Bones_Folder.Mod.Moderation
{
    [Serializable]
    public class Trie : TrieNode
    {
        private static int CurrentID = 0;
        private readonly Dictionary<Token, int> InitialTokens = new();
        private readonly Dictionary<Token, int> FinalTokens = new();

        private string Name;

        [SerializeField]
        private string CachedCapturingRegexPattern;
        [SerializeField]
        private string CachedRegexPattern;

        public bool IsValid
            => IsRoot
            && !IsEnd
            ;

        public Trie()
            : base()
        {
            Name ??= $"{GetType().Name}::{++CurrentID}";
            IsEnd = true;
        }

        public Trie(string Name, IEnumerable<string> Words)
            : this()
        {
            this.Name = Name ?? $"{GetType().Name}::{++CurrentID}";
            try
            {
                if (Words.IsNullOrEmpty())
                    throw new ArgumentException($"Must contain elements", nameof(Words));

                Add(Words);
            }
            catch (Exception x)
            {
                Utils.Warn($"Failed to add {nameof(Words)} to new {nameof(Trie)}", x);
            }
        }

        public Trie(params string[] Words)
            : this(null, Words.IteratorSafe())
        { }

        protected override string DebugName()
            => $"{base.DebugName()}[{Name}]"
            ;

        public override bool Add(Word Word)
        {
            if (base.Add(Word))
            {
                InitialTokens.Increment(Word.First());
                FinalTokens.Increment(Word.Last());
                IsEnd = false;
                ClearPatternCaches();
                //Utils.Log($"{nameof(Trie)}.{nameof(Add)}");
                return true;
            }
            return false;
        }

        public override void OnRemoveWord(Word Word)
        {
            InitialTokens.Decrement(Word.First());
            FinalTokens.Decrement(Word.Last());
            ClearPatternCaches();
        }

        public bool Merge(Trie Other, bool DisposeAfter = false)
        {
            if (Other == null)
                return false;

            bool any = Add(GetWords());

            if (DisposeAfter)
                Other.Dispose();

            return any;
        }

        public IEnumerable<Token> GetInitialTokens()
            => InitialTokens.Keys.IteratorSafe()
            ;

        public IEnumerable<string> GetInitialTokenStrings()
            => GetInitialTokens().Select(t => t.ToString())
            ;

        public IEnumerable<Token> GetFinalTokens()
            => FinalTokens.Keys
            ;

        public IEnumerable<string> GetFinalTokenStrings()
            => GetFinalTokens().Select(t => t.ToString())
            ;

        public void ClearPatternCaches()
        {
            CachedCapturingRegexPattern = null;
            CachedRegexPattern = null;
        }

        public string GetUnboundRegexPattern(bool Capturing = false)
            => Capturing
            ? CachedCapturingRegexPattern ??= $"({this})"
            : CachedRegexPattern ??= $"(?:{this})"
            ;

        public IEnumerable<Word> GetWords(Predicate<Word> Where, Predicate<Token> WhereToken)
        {
            if (IsRoot
                && IsEnd)
                yield break;

            foreach (var node in YieldEndNodes())
            {
                if (node.ToWord(WhereToken) is Word word)
                {
                    if (Where?.Invoke(word) is not false)
                        yield return word;
                    else
                        word.Dispose();
                }
            }
        }

        public IEnumerable<Word> GetWords(Predicate<Word> Where)
            => GetWords(Where, null)
            ;

        public IEnumerable<Word> GetWords(Predicate<Token> WhereToken)
            => GetWords(null, WhereToken)
            ;

        public IEnumerable<Word> GetWords()
            => GetWords(null, null)
            ;

        public IEnumerable<string> GetWordStrings(bool ExcludeSpecialTokens = false)
        {
            foreach (var word in GetWords(WhereToken: ExcludeSpecialTokens ? t => !t.IsSpecial : null))
                yield return (string)word;
        }

        public override void Clear()
        {
            base.Clear();
            Name = null;
            ClearPatternCaches();
            InitialTokens.Clear();
            FinalTokens.Clear();
            IsEnd = true;
        }
    }
}
