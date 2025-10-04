using Anviz.SDK.Responses;
using Anviz.SDK.Utils;
using System;
using System.Collections.Generic;

namespace Anviz.SDK.Users
{
    public sealed class UserProfile
    {
        private readonly List<FaceTemplate> faceTemplates = new List<FaceTemplate>();

        public UserInfo User { get; }
        public IReadOnlyList<FaceTemplate> FaceTemplates => faceTemplates.AsReadOnly();

        public UserProfile(UserInfo user)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
        }

        public UserProfile(UserInfo user, IEnumerable<FaceTemplate> templates) : this(user)
        {
            if (templates == null)
            {
                return;
            }

            foreach (var template in templates)
            {
                AddFaceTemplate(template);
            }
        }

        public void AddFaceTemplate(FaceTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var clone = template.Clone();
            var existingIndex = faceTemplates.FindIndex(t => t.Index == clone.Index);
            if (existingIndex >= 0)
            {
                faceTemplates[existingIndex] = clone;
            }
            else
            {
                faceTemplates.Add(clone);
            }
        }

        public void ClearFaceTemplates()
        {
            faceTemplates.Clear();
        }

        public UserInfo CreateDeviceUser()
        {
            return User.CloneForDevice();
        }
    }
}

