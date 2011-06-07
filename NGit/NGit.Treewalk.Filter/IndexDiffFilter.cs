/*
This code is derived from jgit (http://eclipse.org/jgit).
Copyright owners are documented in jgit's IP log.

This program and the accompanying materials are made available
under the terms of the Eclipse Distribution License v1.0 which
accompanies this distribution, is reproduced below, and is
available at http://www.eclipse.org/org/documents/edl-v10.php

All rights reserved.

Redistribution and use in source and binary forms, with or
without modification, are permitted provided that the following
conditions are met:

- Redistributions of source code must retain the above copyright
  notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above
  copyright notice, this list of conditions and the following
  disclaimer in the documentation and/or other materials provided
  with the distribution.

- Neither the name of the Eclipse Foundation, Inc. nor the
  names of its contributors may be used to endorse or promote
  products derived from this software without specific prior
  written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using NGit.Dircache;
using NGit.Treewalk;
using NGit.Treewalk.Filter;
using Sharpen;

namespace NGit.Treewalk.Filter
{
	/// <summary>
	/// A performance optimized variant of
	/// <see cref="TreeFilter.ANY_DIFF">TreeFilter.ANY_DIFF</see>
	/// which should
	/// be used when among the walked trees there is a
	/// <see cref="NGit.Dircache.DirCacheIterator">NGit.Dircache.DirCacheIterator</see>
	/// and a
	/// <see cref="NGit.Treewalk.WorkingTreeIterator">NGit.Treewalk.WorkingTreeIterator</see>
	/// . Please see the documentation of
	/// <see cref="TreeFilter.ANY_DIFF">TreeFilter.ANY_DIFF</see>
	/// for a basic description of the semantics.
	/// <p>
	/// This filter tries to avoid computing content ids of the files in the
	/// working-tree. In contrast to
	/// <see cref="TreeFilter.ANY_DIFF">TreeFilter.ANY_DIFF</see>
	/// this filter takes
	/// care to first compare the entry from the
	/// <see cref="NGit.Dircache.DirCacheIterator">NGit.Dircache.DirCacheIterator</see>
	/// with the
	/// entries from all other iterators besides the
	/// <see cref="NGit.Treewalk.WorkingTreeIterator">NGit.Treewalk.WorkingTreeIterator</see>
	/// .
	/// Since all those entries have fast access to content ids that is very fast. If
	/// a difference is detected in this step this filter decides to include that
	/// path before even looking at the working-tree entry.
	/// <p>
	/// If no difference is found then we have to compare index and working-tree as
	/// the last step. By making use of
	/// <see cref="NGit.Treewalk.WorkingTreeIterator.IsModified(NGit.Dircache.DirCacheEntry, bool)
	/// 	">NGit.Treewalk.WorkingTreeIterator.IsModified(NGit.Dircache.DirCacheEntry, bool)
	/// 	</see>
	/// we can avoid the computation of the content id if the file is not dirty.
	/// <p>
	/// Instances of this filter should not be used for multiple
	/// <see cref="NGit.Treewalk.TreeWalk">NGit.Treewalk.TreeWalk</see>
	/// s.
	/// Always construct a new instance of this filter for each TreeWalk.
	/// </summary>
	public class IndexDiffFilter : TreeFilter
	{
		private readonly int dirCache;

		private readonly int workingTree;

		private readonly bool honorIgnores;

		/// <summary>Creates a new instance of this filter.</summary>
		/// <remarks>
		/// Creates a new instance of this filter. Do not use an instance of this
		/// filter in multiple treewalks.
		/// </remarks>
		/// <param name="dirCacheIndex">
		/// the index of the
		/// <see cref="NGit.Dircache.DirCacheIterator">NGit.Dircache.DirCacheIterator</see>
		/// in the associated
		/// treewalk
		/// </param>
		/// <param name="workingTreeIndex">
		/// the index of the
		/// <see cref="NGit.Treewalk.WorkingTreeIterator">NGit.Treewalk.WorkingTreeIterator</see>
		/// in the associated
		/// treewalk
		/// </param>
		public IndexDiffFilter(int dirCacheIndex, int workingTreeIndex) : this(dirCacheIndex
			, workingTreeIndex, true)
		{
		}

		/// <summary>Creates a new instance of this filter.</summary>
		/// <remarks>
		/// Creates a new instance of this filter. Do not use an instance of this
		/// filter in multiple treewalks.
		/// </remarks>
		/// <param name="dirCacheIndex">
		/// the index of the
		/// <see cref="NGit.Dircache.DirCacheIterator">NGit.Dircache.DirCacheIterator</see>
		/// in the associated
		/// treewalk
		/// </param>
		/// <param name="workingTreeIndex">
		/// the index of the
		/// <see cref="NGit.Treewalk.WorkingTreeIterator">NGit.Treewalk.WorkingTreeIterator</see>
		/// in the associated
		/// treewalk
		/// </param>
		/// <param name="honorIgnores">
		/// true if the filter should skip working tree files that are
		/// declared as ignored by the standard exclude mechanisms..
		/// </param>
		public IndexDiffFilter(int dirCacheIndex, int workingTreeIndex, bool honorIgnores
			)
		{
			this.dirCache = dirCacheIndex;
			this.workingTree = workingTreeIndex;
			this.honorIgnores = honorIgnores;
		}

		/// <exception cref="NGit.Errors.MissingObjectException"></exception>
		/// <exception cref="NGit.Errors.IncorrectObjectTypeException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		public override bool Include(TreeWalk tw)
		{
			// If the working tree file doesn't exist, it does exist for at least
			// one other so include this difference.
			int wm = tw.GetRawMode(workingTree);
			if (wm == 0)
			{
				return true;
			}
			// If the path does not appear in the DirCache and its ignored
			// we can avoid returning a result here, but only if its not in any
			// other tree.
			int cnt = tw.TreeCount;
			int dm = tw.GetRawMode(dirCache);
			if (dm == 0)
			{
				if (honorIgnores && WorkingTree(tw).IsEntryIgnored())
				{
					int i = 0;
					for (; i < cnt; i++)
					{
						if (i == dirCache || i == workingTree)
						{
							continue;
						}
						if (tw.GetRawMode(i) != 0)
						{
							break;
						}
					}
					// If i is cnt then the path does not appear in any other tree,
					// and this working tree entry can be safely ignored.
					return i == cnt ? false : true;
				}
				else
				{
					// In working tree and not ignored, and not in DirCache.
					return true;
				}
			}
			// Always include subtrees as WorkingTreeIterator cannot provide
			// efficient elimination of unmodified subtrees.
			if (tw.IsSubtree)
			{
				return true;
			}
			// Try the inexpensive comparisons between index and all real trees
			// first. Only if we don't find a diff here we have to bother with
			// the working tree
			for (int i_1 = 0; i_1 < cnt; i_1++)
			{
				if (i_1 == dirCache || i_1 == workingTree)
				{
					continue;
				}
				if (tw.GetRawMode(i_1) != dm || !tw.IdEqual(i_1, dirCache))
				{
					return true;
				}
			}
			// Only one chance left to detect a diff: between index and working
			// tree. Make use of the WorkingTreeIterator#isModified() method to
			// avoid computing SHA1 on filesystem content if not really needed.
			WorkingTreeIterator wi = WorkingTree(tw);
			DirCacheIterator di = tw.GetTree<DirCacheIterator>(dirCache);
			return wi.IsModified(di.GetDirCacheEntry(), true);
		}

		private WorkingTreeIterator WorkingTree(TreeWalk tw)
		{
			return tw.GetTree<WorkingTreeIterator>(workingTree);
		}

		public override bool ShouldBeRecursive()
		{
			// We cannot compare subtrees in the working tree, so encourage
			// use of recursive walks where the subtrees are always dived into.
			return true;
		}

		public override TreeFilter Clone()
		{
			return this;
		}

		public override string ToString()
		{
			return "INDEX_DIFF_FILTER";
		}
	}
}
