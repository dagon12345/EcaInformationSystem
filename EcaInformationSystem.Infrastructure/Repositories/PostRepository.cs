using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities.PostEntities;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class PostRepository : IPostRepository
    {
        private readonly AppDbContext _context;
        private const string SuperAdminRole = "SuperAdmin";
        public PostRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task RemoveImagesAsync(Guid postId, List<Guid> imageIds)
        {
            var images = await _context.PostImages
                .Where(i => i.PostId == postId && imageIds.Contains(i.Id))
                .ToListAsync();
            _context.PostImages.RemoveRange(images);
        }

        public async Task<List<PostImageDto>> GetImageMetaAsync(Guid postId)
            => await _context.PostImages.AsNoTracking()
                .Where(i => i.PostId == postId)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new PostImageDto { Id = i.Id, DisplayOrder = i.DisplayOrder, Width = i.Width, Height = i.Height })
                .ToListAsync();
        public async Task AddImagesAsync(List<PostImage> images) => await _context.PostImages.AddRangeAsync(images);
        public async Task<PostImage?> GetImageEntityAsync(Guid imageId)
            => await _context.PostImages.FirstOrDefaultAsync(i => i.Id == imageId);
        public async Task<PagedResultDto<PostDto>> GetFeedAsync(int pageNumber, int pageSize, string? viewerKey, Guid? viewerUserId, string? viewerRole)
        {
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var baseQuery = _context.Posts.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt);

            var totalCount = await baseQuery.CountAsync();

            var page = await baseQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var postIds = page.Select(p => p.Id).ToList();

            // ── Counts — grouped in one shot each, no per-post round trip ──────
            var likeCounts = await _context.PostLikes.AsNoTracking()
                .Where(l => postIds.Contains(l.PostId))
                .GroupBy(l => l.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToListAsync();

            var commentCounts = await _context.PostComments.AsNoTracking()
                .Where(c => postIds.Contains(c.PostId) && !c.IsDeleted)
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToListAsync();

            var viewCounts = await _context.PostViews.AsNoTracking()
                .Where(v => postIds.Contains(v.PostId))
                .GroupBy(v => v.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToListAsync();

            var likedByViewer = new HashSet<Guid>();
            if (!string.IsNullOrWhiteSpace(viewerKey))
            {
                likedByViewer = (await _context.PostLikes.AsNoTracking()
                    .Where(l => postIds.Contains(l.PostId) && l.LikerKey == viewerKey)
                    .Select(l => l.PostId)
                    .ToListAsync()).ToHashSet();
            }

            bool isSuperAdmin = viewerRole == SuperAdminRole;


            // ✅ NEW — image metadata only, no ImageData/ThumbnailData bytes here.
            // The grid just needs dimensions to lay out correctly; actual bytes are
            // fetched lazily per-image via the dedicated image endpoint below.
            var imageMeta = await _context.PostImages.AsNoTracking()
                .Where(i => postIds.Contains(i.PostId))
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new { i.Id, i.PostId, i.DisplayOrder, i.Width, i.Height })
                .ToListAsync();


            var items = page.Select(p => new PostDto
            {
                Id = p.Id,
                AuthorUserId = p.AuthorUserId,
                AuthorName = p.AuthorName,
                AuthorPosition = p.AuthorPosition,
                AuthorRegion = p.AuthorRegion,
                Content = p.Content,
                CreatedAt = p.CreatedAt,
                LikeCount = likeCounts.FirstOrDefault(x => x.PostId == p.Id)?.Count ?? 0,
                CommentCount = commentCounts.FirstOrDefault(x => x.PostId == p.Id)?.Count ?? 0,
                ViewCount = viewCounts.FirstOrDefault(x => x.PostId == p.Id)?.Count ?? 0,
                IsLikedByViewer = likedByViewer.Contains(p.Id),
                CanDelete = viewerUserId.HasValue &&
                    (viewerUserId.Value == p.AuthorUserId || isSuperAdmin),
                Images = imageMeta
                      .Where(i => i.PostId == p.Id)
                      .Select(i => new PostImageDto
                      {
                          Id = i.Id,
                          DisplayOrder = i.DisplayOrder,
                          Width = i.Width,
                          Height = i.Height
                      })
                      .ToList()
            }).ToList();

            return new PagedResultDto<PostDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<Post?> GetEntityByIdAsync(Guid id)
            => await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        public async Task AddPostAsync(Post post)
            => await _context.Posts.AddAsync(post);

        public async Task<(bool isLiked, int likeCount)> ToggleLikeAsync(Guid postId, string likerKey, bool isAnonymous)
        {
            var existing = await _context.PostLikes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.LikerKey == likerKey);

            bool nowLiked;

            if (existing != null)
            {
                _context.PostLikes.Remove(existing);
                nowLiked = false;
            }
            else
            {
                _context.PostLikes.Add(new PostLike
                {
                    Id = Guid.NewGuid(),
                    PostId = postId,
                    LikerKey = likerKey,
                    IsAnonymous = isAnonymous,
                    CreatedAt = DateTime.UtcNow
                });
                nowLiked = true;
            }

            await _context.SaveChangesAsync();
            var count = await _context.PostLikes.CountAsync(l => l.PostId == postId);
            return (nowLiked, count);
        }

        public async Task RecordViewAsync(Guid postId, string viewerKey)
        {
            var exists = await _context.PostViews
                .AnyAsync(v => v.PostId == postId && v.ViewerKey == viewerKey);
            if (exists) return;

            _context.PostViews.Add(new PostView
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                ViewerKey = viewerKey,
                ViewedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // ✅ Two tabs viewing at once can race past the AnyAsync check —
                // the unique index catches it, we just swallow the duplicate.
            }
        }

        public async Task<int> GetViewCountAsync(Guid postId)
            => await _context.PostViews.CountAsync(v => v.PostId == postId);

        public async Task<PagedResultDto<PostCommentDto>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize, Guid? viewerUserId, string? viewerRole)
        {
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 20 : pageSize;

            var query = _context.PostComments.AsNoTracking()
                .Where(c => c.PostId == postId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt);

            var totalCount = await query.CountAsync();

            bool isSuperAdmin = viewerRole == SuperAdminRole;

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new PostCommentDto
                {
                    Id = c.Id,
                    PostId = c.PostId,
                    UserId = c.UserId,
                    AuthorName = c.AuthorName,
                    AuthorPosition = c.AuthorPosition,
                    AuthorRegion = c.AuthorRegion,
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    EditedAt = c.EditedAt, //New
                    CanDelete = viewerUserId.HasValue && (viewerUserId.Value == c.UserId || isSuperAdmin),
                    CanEdit = viewerUserId.HasValue && (viewerUserId.Value == c.UserId || isSuperAdmin) //New
                })
                .ToListAsync();

            return new PagedResultDto<PostCommentDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task AddCommentAsync(PostComment comment)
            => await _context.PostComments.AddAsync(comment);

        public async Task<PostComment?> GetCommentEntityByIdAsync(Guid commentId)
            => await _context.PostComments.FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

        public Task SoftDeletePostAsync(Post post, string deletedBy)
        {
            post.IsDeleted = true;
            post.DeletedBy = deletedBy;
            post.DeletedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task SoftDeleteCommentAsync(PostComment comment, string deletedBy)
        {
            comment.IsDeleted = true;
            comment.DeletedBy = deletedBy;
            comment.DeletedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}