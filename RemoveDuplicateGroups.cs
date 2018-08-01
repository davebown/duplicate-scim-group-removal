using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Collabco.Myday.Scim;

namespace Remove_duplicate_groups
{
    public partial class RemoveDuplicateGroups : Form
    {
        private readonly SynchronizationContext synchronizationContext;

        public RemoveDuplicateGroups()
        {
            InitializeComponent();
            synchronizationContext = SynchronizationContext.Current;
        }

        //Dictionary of groups by externalId
        Dictionary<string, Collabco.Myday.Scim.v2.Model.ScimGroup2> groups = new Dictionary<string, Collabco.Myday.Scim.v2.Model.ScimGroup2>();

        //List of duplicate group ids
        List<string> duplicateGroupIds = new List<string>();

        private async void btn_IdentityDuplicates_Click(object sender, EventArgs e)
        {
            groups.Clear();
            duplicateGroupIds.Clear();
            var scimClient = CreateScimClient();

            await Task.Run(() => FindDuplicateScimGroups(scimClient));

            MessageBox.Show("Duplicate search completed", "Operation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task FindDuplicateScimGroups(ScimClient scimClient)
        {
            var totalGroupCount = 0;
            Collabco.Myday.Scim.v2.Model.ScimListResponse2<Collabco.Myday.Scim.v2.Model.ScimGroup2> groupsPage = null;
            var startIndex = 1;
            while (groupsPage == null || groupsPage.TotalResults > totalGroupCount)
            {
                groupsPage = await scimClient.Search<Collabco.Myday.Scim.v2.Model.ScimGroup2>(
                    new Collabco.Myday.Scim.Query.ScimQueryOptions
                    {
                        Attributes = new List<string> { "id", "externalId", "meta.created", "meta.lastModified" },
                        StartIndex = startIndex,
                        SortBy = "meta.created",
                        SortOrder = Collabco.Myday.Scim.Core.Model.SortOrder.Ascending
                    }
                );

                totalGroupCount += groupsPage.Resources.Count();                
                startIndex += groupsPage.ItemsPerPage;

                foreach(var group in groupsPage.Resources)
                {
                    if(groups.ContainsKey(group.ExternalId))
                    {
                        duplicateGroupIds.Add(group.Id);
                    }
                    else
                    {
                        groups.Add(group.ExternalId, group);
                    }
                }

                UpdateDuplicateCounts(totalGroupCount);
            }
        }


        private void UpdateDuplicateCounts(int totalGroupCount)
        {
            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                lbl_TotalGroups.Text = o.ToString();
                lbl_NoDuplicates.Text = duplicateGroupIds.Count.ToString();
                lbl_NoUniqueGroups.Text = groups.Count.ToString();
            }), totalGroupCount);
        }

        private async void btn_Delete_Click(object sender, EventArgs e)
        {
            var scimClient = CreateScimClient();

            await Task.Run(async () => 
            {
                var deleteCount = 0;

                foreach(var groupId in duplicateGroupIds)
                {
                    await scimClient.Delete<Collabco.Myday.Scim.v2.Model.ScimGroup2>(groupId);
                    deleteCount++;
                    UpdateDeleteCount(deleteCount);
                }
            });

            MessageBox.Show("Delete duplicates completed", "Operation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateDeleteCount(int deleteCount)
        {
            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                lbl_NoDeleted.Text = o.ToString();
            }), deleteCount);
        }

        private ScimClient CreateScimClient()
        {
            var scimConfig = new Collabco.Myday.Scim.Configuration.ScimConfiguration
            {
                BaseUrl = $"https://scim.myday.cloud/{txt_TenantId.Text}/v2",
                DefaultSearchPageSize = 50
            };

            scimConfig.AddDefaultScimResourceTypes();
            scimConfig.AddMydayScimResourceTypes();

            return new ScimClient(scimConfig, txt_Token.Text);
        }
    }
}
