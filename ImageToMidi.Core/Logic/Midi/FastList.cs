using System;
using System.Collections;
using System.Collections.Generic;

namespace ImageToMidi.Logic.Midi
{
    public class FastList<T> : IEnumerable<T>
    {
        private class Node
        {
            public T? Data;
            public Node? Next;
        }

        private Node? head;
        private Node? tail;
        private int count;

        public bool ZeroLen => count == 0;

        public void Add(T item)
        {
            var newNode = new Node { Data = item };
            if (head == null)
            {
                head = tail = newNode;
            }
            else
            {
                if (tail != null)
                    tail.Next = newNode;
                tail = newNode;
            }
            count++;
        }

        public T? Pop()
        {
            if (head == null) throw new InvalidOperationException("List is empty");
            T? data = head.Data;
            head = head.Next;
            if (head == null) tail = null;
            count--;
            return data;
        }

        public void Unlink()
        {
            head = null;
            tail = null;
            count = 0;
        }

        public int Count()
        {
            return count;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var current = head;
            while (current != null)
            {
                if (current.Data != null)
                    yield return current.Data;
                current = current.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
