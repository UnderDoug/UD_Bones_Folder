using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Collections;
using XRL.World;

using SerializeField = UnityEngine.SerializeField;

namespace UD_Bones_Folder.Mod.Moderation
{
    [Serializable]
    public class TrieNode : IComposite, IDisposable
    {
        public class Word : IEnumerable<Token>, IDisposable
        {
            private Rack<Token> Tokens;
            private Rack<Token> ConsumedTokens;

            public Word()
            { }

            public Word(IEnumerable<Token> Tokens)
                : this()
            {
                if (Tokens == null)
                    throw new ArgumentNullException(nameof(Tokens));

                if (Tokens.Count() < 1)
                    throw new ArgumentOutOfRangeException(nameof(Tokens), "Cannot be empty");

                this.Tokens = new(Tokens.Count());
                this.Tokens.AddRange(Tokens);
                ConsumedTokens = new();
            }

            public Word(string String)
                : this(GetTokens(String))
            { }

            public Word(Word Word)
                : this((string)Word)
            { }

            public static IEnumerable<Token> GetTokens(string String)
            {
                string remaining = String;
                int ticker = 0;
                string prevRemaining = null;
                bool isInvalid = false;
                using var tokens = ScopeDisposedList<Token>.GetFromPool();
                while (Token.TryGetNext(remaining, out var token, out remaining, out isInvalid)
                    && ticker++ <= String.Length + 2
                    && prevRemaining != remaining
                    && !isInvalid)
                {
                    prevRemaining = remaining;
                    tokens.Add(token);
                }

                if (isInvalid)
                    yield break;

                if (ticker > String.Length)
                    yield break;

                if (prevRemaining == remaining
                    && remaining != null)
                    yield break;

                if (tokens.IsNullOrEmpty())
                    yield break;

                foreach (var token in tokens)
                    yield return token;
            }

            public static Word FromString(string String)
                => GetTokens(String) is IEnumerable<Token> tokens
                    && !tokens.IsNullOrEmpty()
                ? new Word(tokens)
                : null
                ;

            public override string ToString()
                => !Tokens.IsNullOrEmpty()
                    || !ConsumedTokens.IsNullOrEmpty()
                ? $"{ConsumedTokens.IteratorSafe().Aggregate((string)null, (a, n) => a + n)}{Tokens.IteratorSafe().Aggregate((string)null, (a, n) => a + n)}"
                : "empty tokens"
                ;

            public IEnumerator<Token> GetEnumerator()
                => Tokens.IteratorSafe().GetEnumerator()
                ;

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator()
                ;

            public Token First()
            {
                if (!ConsumedTokens.IsNullOrEmpty())
                    return ConsumedTokens.FirstOrDefault();

                if (!Tokens.IsNullOrEmpty())
                    return Tokens.FirstOrDefault();

                return default;
            }

            public Token Last()
            {
                if (!Tokens.IsNullOrEmpty())
                    return Tokens.LastOrDefault();

                if (!ConsumedTokens.IsNullOrEmpty())
                    return ConsumedTokens.LastOrDefault();

                return default;
            }

            public Token TakeNext()
            {
                var token = Tokens.TakeAt(0);
                ConsumedTokens.Add(token);
                return token;
            }

            public Token Current()
                => ConsumedTokens.LastOrDefault()
                ;

            public Token TakePrev()
            {
                var token = ConsumedTokens.TakeAt(0);
                Tokens.Insert(0, token);
                return token;
            }

            public void Dispose()
            {
                Tokens?.Clear();
                Tokens = null;
                ConsumedTokens?.Clear();
                ConsumedTokens = null;
            }

            public static explicit operator string(Word Word)
                => Word?.ToString()
                ;

            public static explicit operator Word(string String)
                => String != string.Empty
                ? FromString(String)
                : null
                ;
        }

        public struct NodeInfo : IDisposable
        {
            public enum NodeType
            {
                Single, // Single Character: a
                CharClass, // Character Class: [abc] 
                Group, // Group: (ab|ac|abc)
            }
            public TrieNode Node;
            public NodeType Type;

            public override readonly string ToString()
                => Node.ToString()
                ;

            public void Dispose()
            {
                Node = null;
                Type = NodeType.Single;
            }
        }

        [Serializable]
        public struct Token : IEquatable<Token>, IComposite, IDisposable
        {
            public static char[] RegexTokens = new char[3]
            {
                '?', // Zero or one
                '*', // Zero or more
                '+', // One or more
            };

            public string Value;
            public Range Count;

            public readonly bool Repeats => IsRepeating(Count);
            public readonly bool Optional => IsOptional(Count);
            public readonly bool Lazy => IsLazy(Count);
            public readonly bool Greedy => IsGreedy(Count);

            public readonly bool LazyRepeating => IsLazyRepeating(Count);
            public readonly bool NonRepeatingOptional => IsNonRepeatingOptional(Count);

            private static bool IsRepeating(Range Count)
                => !Count.End.Equals(1)
                ;

            private static bool IsOptional(Range Count)
                => Count.Start.Equals(0)
                ;

            private static bool IsLazy(Range Count)
                => (IsRepeating(Count)
                    || IsOptional(Count))
                && Count.End.Equals(^1)
                ;

            private static bool IsGreedy(Range Count)
                => (IsRepeating(Count)
                    || IsOptional(Count))
                && Count.End.Equals(Index.End)
                ;

            private static bool IsLazyRepeating(Range Count) // "*?" or "+?"
                => IsRepeating(Count)
                && IsLazy(Count)
                ;

            private static bool IsNonRepeatingOptional(Range Count) // "?"
                => IsOptional(Count)
                && !IsRepeating(Count)
                ;

            private static void ProcessChar(
                ref string Value,
                ref string Consumed,
                ref char CurrentChar,
                ref char PrevChar,
                bool SkipValue = false
                )
            {
                if (!SkipValue)
                    Value += CurrentChar;
                PrevChar = CurrentChar;
                Consumed += CurrentChar;
            }

            private static Range MakeLazyOrOptional(Range Count)
            {
                return new Range(
                    start: IsGreedy(Count) ? Count.Start : 0,
                    end: ^1);
            }

            public static bool TryGetNext(string Text, out Token Token, out string Remaining, out bool Invalid)
            {
                Invalid = false;
                Token = default;
                Remaining = Text;
                if (Remaining.IsNullOrEmpty())
                    return false;

                bool escaped = false;
                string value = null;
                var count = 1..1;
                char prevChar = default;
                string consumed = null;
                for (int i = 0; i < Text.Length; i++)
                {
                    bool firstIteration = i == 0;
                    if (Text[i] is char currentChar)
                    {
                        if (!escaped
                            && consumed.IsNullOrEmpty())
                        {
                            escaped = currentChar == '\\';
                            if (escaped)
                            {
                                ProcessChar(
                                    Value: ref value,
                                    Consumed: ref consumed,
                                    CurrentChar: ref currentChar,
                                    PrevChar: ref prevChar);
                                continue;
                            }
                        }

                        if (RegexTokens.Contains(currentChar))
                        {
                            if (firstIteration)
                            {
                                Utils.Warn($"{nameof(Trie)}.{nameof(Token)}.{nameof(TryGetNext)} cannot produce token starting with unescaped '{Text[0]}'; Aborting...");
                                Invalid = true;
                                return false;
                            }

                            bool isLazyRepeating = IsLazyRepeating(count); // "*?" or "+?"
                            bool isNonRepeatingOptional = IsNonRepeatingOptional(count); // "?"
                            if (isLazyRepeating
                                || isNonRepeatingOptional)
                            {
                                string reason = isLazyRepeating
                                    ? nameof(IsLazyRepeating)
                                    : nameof(IsNonRepeatingOptional)
                                    ;

                                Utils.Warn($"{nameof(Trie)}.{nameof(Token)}.{nameof(TryGetNext)} invalid sequence: \"{prevChar}{currentChar}\", {reason}; Aborting...");
                                Invalid = true;
                                return false;
                            }
                            if (escaped)
                            {
                                escaped = false;
                                ProcessChar(
                                    Value: ref value,
                                    Consumed: ref consumed,
                                    CurrentChar: ref currentChar,
                                    PrevChar: ref prevChar);
                                continue;
                            }
                            count = currentChar switch
                            {
                                '?' => MakeLazyOrOptional(count),
                                '*' => 0..Index.End,
                                '+' => 1..Index.End,
                                _ => count,
                            };

                            ProcessChar(
                                Value: ref value,
                                Consumed: ref consumed,
                                CurrentChar: ref currentChar,
                                PrevChar: ref prevChar,
                                SkipValue: true);
                            continue;
                        }

                        if (!consumed.IsNullOrEmpty())
                        {
                            if (!escaped)
                                break;
                        }

                        ProcessChar(
                            Value: ref value,
                            Consumed: ref consumed,
                            CurrentChar: ref currentChar,
                            PrevChar: ref prevChar);

                        escaped = false;
                    }
                }

                int consumedLength = Math.Clamp(consumed.Length, 0, Remaining.Length);
                if (consumedLength >= Remaining.Length)
                    Remaining = null;
                else
                    Remaining = Remaining[consumedLength..];

                if (value.IsNullOrEmpty())
                {
                    Invalid = true;
                    return false;
                }

                Token = new Token
                {
                    Value = value,
                    Count = count,
                };
                return true;
            }

            private static string RangeToString(Range Count)
            {
                string output = null;
                if (IsRepeating(Count))
                {
                    if (IsOptional(Count))
                        output = "*";
                    else
                        output = "+";
                }
                if (IsLazy(Count)
                    || IsNonRepeatingOptional(Count))
                    output += "?";

                return output;
            }

            public override readonly string ToString()
                => !Value.IsNullOrEmpty()
                ? $"{Value}{RangeToString(Count)}"
                : null
                ;

            public override readonly bool Equals(object obj)
                => obj is Token tokenObj
                ? Equals(tokenObj)
                : base.Equals(obj)
                ;

            public override readonly int GetHashCode()
                => (Value?.GetHashCode() ?? 0)
                ^ Count.GetHashCode()
                ;

            public readonly bool Equals(Token other)
                => Value == other.Value
                && Count.Equals(other.Count)
                ;

            public void Dispose()
            {
                Value = null;
                Count = default;
            }

            public static bool operator ==(Token left, Token right)
                => left.Equals(right)
                ;

            public static bool operator !=(Token left, Token right)
                => !(left == right)
                ;
        }

        public static Dictionary<string, string> Alternatives = new()
            {
                { "@", "a" },
                { "4", "a" },
                { "6", "b" },
                { "3", "e" },
                { "9", "g" },
                { "1", "i" },
                { "!", "i" },
                { "5", "s" },
                { "7", "t" },
            };

        [SerializeField]
        private TrieNode _Parent;
        public TrieNode Parent => _Parent;

        public bool IsRoot => Parent == null;

        public Token Value;
        private readonly Dictionary<Token, TrieNode> Children = new();

        public bool IsEnd;

        public bool Repeats;

        public int Count
            => Children.Count
            ;

        private NodeInfo? NodeInfoCache;
        private IEnumerable<NodeInfo> ChildNodeInfoCache;

        public TrieNode()
        { }

        public TrieNode(Token Value, TrieNode Parent)
            : this()
        {
            this.Value = Value;
            _Parent = Parent;
        }

        public virtual TrieNode this[Token Token]
        {
            get
            {
                if (Children.ContainsKey(Token))
                    return Children[Token];

                return null;
            }
            set
            {
                if (value == null)
                {
                    if (TryGetValue(Token, out var child))
                        child.Clear();

                    Children.Remove(Token);
                }
                else
                {
                    Children[Token] = value;

                }
            }
        }

        public bool Contains(Word Word)
        {
            if (Word.IsNullOrEmpty())
                return false;

            TrieNode node = this;
            foreach (var token in Word)
            {
                if (node[token] is not TrieNode child)
                    return false;

                node = child;
                if (node.IsEnd)
                    return token == Word.Last();
            }
            return false;
        }

        public bool Contains(IEnumerable<Token> Tokens)
        {
            if (Tokens.IsNullOrEmpty())
                return false;

            using var word = new Word(Tokens);
            return Contains(word);
        }

        public virtual bool Contains(string Word)
            => Contains((Word)Word)
            ;

        public virtual bool TryGetValue(Token Token, out TrieNode Node)
            => Children.TryGetValue(Token, out Node)
            ;

        protected virtual string DebugName()
        {
            string name = $"{GetType().Name}";

            bool isRoot = IsRoot;

            if (isRoot)
                name += $"({nameof(IsRoot)}";

            if (Value.ToString() is string value)
            {
                if (isRoot)
                    name += ", ";
                else
                    name += "(";

                name += $"{nameof(Value)}: {value}, ";
            }
            else
            if (!isRoot)
                name += "(";
            else
                name += ", ";

            name += $"{Count}{(IsEnd ? $", {nameof(IsEnd)}" : null)})";

            return name;
        }

        protected TrieNode Require(Word Word)
        {
            if (!Word.IsNullOrEmpty()
                && Word.TakeNext() is Token token
                && token != default)
            {
                if (!TryGetValue(token, out TrieNode child))
                {
                    child = this[token] = new(token, this);
                    ClearNodeInfoCaches();
                }
                return child.Require(Word);
            }
            IsEnd = true;
            return this;
        }

        public virtual bool Add(Word Word)
        {
            if (Word.IsNullOrEmpty())
                return false;

            if (Contains(Word))
                return false;

            return Require(Word).IsEnd;
        }

        public bool Add(IEnumerable<Word> Words)
        {
            Words = Words.IteratorSafe().OrderBy(w => (string)w);
            bool any = false;
            foreach (var word in Words)
            {
                Add(word);
                any = true;
            }
            return any;
        }

        public virtual bool Add(IEnumerable<string> Words)
        {
            Words = Words.IteratorSafe().OrderBy(s => s);
            bool any = false;
            foreach (var stringWord in Words)
            {
                using var word = Word.FromString(stringWord);
                if (!word.IsNullOrEmpty())
                    if (Add(word))
                        any = true;
            }
            return any;
        }

        public void Add(params string[] Words)
            => Add(Words.IteratorSafe())
            ;

        public virtual void OnRemoveWord(Word Word)
        {
        }

        public virtual bool Remove(Word Word)
        {
            if (Word.IsNullOrEmpty())
                return false;

            if (!Contains(Word))
                return false;

            bool removeWord = false;
            var tokens = Word.ToList();
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                bool isLastToken = i == tokens.Count - 1;

                if (isLastToken
                    && Contains(Word))
                {
                    removeWord = true;
                    OnRemoveWord(Word);
                }

                if (!removeWord)
                    break;

                TrieNode node = this;
                for (int j = 0; j < i - 1; j++)
                    node = node[tokens[j]];

                var penultToken = tokens[i - 1];
                var penultNode = node[penultToken];

                if (isLastToken)
                {
                    if (penultNode.IsEnd)
                    {
                        if (penultNode.Count == 0)
                            node[penultToken] = null;
                        else
                            penultNode.IsEnd = false;
                    }
                }
                else
                if (penultNode.Count == 0)
                    node[penultToken] = null;
            }
            ClearNodeInfoCaches();
            return true;
        }

        public bool Remove(IEnumerable<Word> Words)
        {
            bool any = false;

            foreach (var word in Words.IteratorSafe())
                if (Remove(word))
                    any = true;

            return any;
        }

        public bool Remove(IEnumerable<string> Words)
        {
            bool any = false;
            foreach (var stringWord in Words.IteratorSafe())
            {
                using var word = Word.FromString(stringWord);
                if (Remove(word))
                    any = true;
            }
            return any;
        }

        private IEnumerable<TrieNode> YieldNodesInternal()
        {
            yield return this;

            foreach (var childNode in Children.Values.IteratorSafe())
                foreach (var subNode in childNode.YieldNodesInternal())
                    yield return subNode;
        }

        public IEnumerable<TrieNode> YieldNodes(Predicate<TrieNode> Where = null)
        {
            foreach (var node in YieldNodesInternal())
                if (Where?.Invoke(node) is not false)
                    yield return node;
        }

        public IEnumerable<TrieNode> YieldEndNodes()
            => YieldNodes(Where: n => n.IsEnd)
            ;

        public NodeInfo GetNodeInfo(out IEnumerable<NodeInfo> ChildInfos)
        {
            ChildInfos = GetChildrenInfo();
            return NodeInfoCache ??= new NodeInfo
            {
                Node = this,
                Type = GetNodeType(ChildInfos),
            };
        }

        public NodeInfo GetNodeInfo()
            => GetNodeInfo(out _)
            ;

        public NodeInfo.NodeType GetNodeType(IEnumerable<NodeInfo> NodeInfos)
        {
            if (NodeInfos.IsNullOrEmpty()
                && !Value.Repeats
                && !Value.Optional)
                return NodeInfo.NodeType.Single;

            if (NodeInfos.All(n => n.Type == NodeInfo.NodeType.Single)
                && !IsEnd)
                return NodeInfo.NodeType.CharClass;
            else
                return NodeInfo.NodeType.Group;
        }

        private IEnumerable<NodeInfo> GetChildrenInfoInternal()
        {
            int pos = 0;
            foreach (var child in Children.Values.IteratorSafe())
            {
                if (child == this)
                {
                    Utils.Warn($"{DebugName()} contains itself at {nameof(pos)} {pos}.");
                    continue;
                }
                yield return child.GetNodeInfo();
                pos++;
            }
        }

        public IEnumerable<NodeInfo> GetChildrenInfo()
            => ChildNodeInfoCache ??= GetChildrenInfoInternal();

        private string AsNonCaptureGroup(string String)
        {
            if (String.IsNullOrEmpty())
                return null;

            if (String.Length == 1)
                return String;

            if (String.Length == 2
                && String.StartsWith("\\"))
                return String;

            return $"(?:{String})";
        }

        private string AsCharClass(string String)
        {
            if (String.IsNullOrEmpty())
                return null;

            if (String.Length == 1)
                return String;

            if (String.Length == 2
                && String.StartsWith("\\"))
                return String;

            return $"[{String}]";
        }

        public string GetChildrenString()
        {
            var nodeInfo = GetNodeInfo(out var childInfos);
            if (nodeInfo.Type == NodeInfo.NodeType.Single
                || childInfos.IsNullOrEmpty())
                return null;

            var type = nodeInfo.Type;
            string output = childInfos.Aggregate(
                seed: (string)null,
                func: delegate (string acc, NodeInfo next)
                {
                    if (type == NodeInfo.NodeType.CharClass)
                        return acc + next;

                    return Utils.PipeDelimitedAggregator(acc, next);
                });

            if (output.IsNullOrEmpty())
                return null;

            if (childInfos.Count() == 1)
                return output;

            if (type == NodeInfo.NodeType.CharClass)
                return AsCharClass(output);

            return AsNonCaptureGroup(output);
        }

        public override string ToString()
        {
            string childString = GetChildrenString();

            if (!IsRoot
                && IsEnd
                && !childString.IsNullOrEmpty())
                childString = $"(?:{childString})?";

            string output = $"{Value}{childString}";
            if (Parent?.IsRoot is true)
                output = $"(?:{output})+";

            return output;
        }

        public string ToPlainString()
            => $"{Parent?.ToPlainString()}{Value}"
            ;

        public IEnumerable<Token> YieldParentTokens()
        {
            if (Parent != null)
                foreach (var token in Parent.YieldParentTokens())
                    yield return token;

            yield return Value;
        }

        public Word ToWord()
            => YieldParentTokens() is IEnumerable<Token> tokens
                && !tokens.IsNullOrEmpty()
            ? new Word(tokens)
            : null
            ;

        protected void ClearNodeInfoCaches()
        {
            NodeInfoCache = null;
            ChildNodeInfoCache = null;
            foreach (var child in Children.Values.IteratorSafe())
                child.ClearNodeInfoCaches();
        }

        public virtual void Clear()
        {
            _Parent = null;

            Value = default;

            foreach (var child in Children.Values)
                child.Clear();

            Children.Clear();
        }

        public virtual void Dispose()
        {
            Clear();
        }
    }
}
